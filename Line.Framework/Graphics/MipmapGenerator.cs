namespace Line.Framework.Graphics;

/// <summary>
/// mip 层上传回调。
/// <para>
/// <b>为什么不用 <see cref="Action{T}"/></b>：<see cref="ReadOnlySpan{T}"/>
/// 是 ref struct，不能作为泛型类型实参传给普通委托
/// （需要 C# 13 的 <c>allows ref struct</c> 反约束）。
/// 自定义委托不受这个限制。
/// </para>
/// </summary>
/// <param name="data">该层的紧密排列像素数据。</param>
/// <param name="mipLevel">层号，从 1 开始。</param>
/// <param name="width">该层宽度。</param>
/// <param name="height">该层高度。</param>
public delegate void MipUploadCallback(
    ReadOnlySpan<byte> data,
    uint mipLevel,
    uint width,
    uint height
);

/// <summary>
/// CPU 端 mip 链生成。
/// <para>
/// <b>为什么必须有它</b>：创建纹理时申请了 N 层 mip、却只上传第 0 层，
/// 第 1 层及以下是<b>未初始化的显存</b>。采样器一旦落到这些层，读到的内容
/// 完全不确定 —— 表现为「贴图发淡」「随旋转闪烁」「小图整块消失」。
/// 这类症状极难定位，因为它们在代码层面完全合法。
/// </para>
/// <para>
/// <b>只支持紧密排列的 4 字节格式</b>：UI 纹理统一用
/// <see cref="PixelFormat.R8G8B8A8UNorm"/>，对它按 2×2 box filter 求平均
/// 是精确正确的。其他格式一律返回失败，由调用方降级为单层 ——
/// 写一个「看起来通用、实际只对 RGBA8 正确」的实现比不做更危险。
/// </para>
/// <para>
/// <b>不做 sRGB 校正</b>：框架当前所有纹理都是 UNorm（非 sRGB 格式），
/// 直接对存储值求平均与 GPU 的 mip 生成行为一致。将来引入 sRGB 格式时
/// 这里必须改成「先线性化再平均」。
/// </para>
/// </summary>
public static class MipmapGenerator
{
    /// <summary>
    /// 该格式能否生成 mip 链。
    /// <para>
    /// 目前只接受 4 字节/像素的格式（RGBA8 / BGRA8 系列）。
    /// 调用方应当在创建纹理<b>之前</b>用它决定要不要申请多层 mip。
    /// </para>
    /// </summary>
    public static bool IsSupported(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8G8B8A8UNorm
            or PixelFormat.B8G8R8A8UNorm
            or PixelFormat.R8G8B8A8SNorm
            or PixelFormat.R8G8B8A8UInt
            or PixelFormat.R8G8B8A8SInt => true,
            _ => false,
        };
    }

    /// <summary>
    /// 基础层某一维在指定 mip 层上的尺寸：<c>max(1, base &gt;&gt; level)</c>。
    /// <para>
    /// 后端实现 <see cref="ITexture.Update"/> 时用它计算默认区域大小 ——
    /// <b>必须按目标 mip 层算，不能按基础层</b>。用基础层尺寸去校验
    /// mip1 的数据长度，会被判定为「长度不足」（mip1 只有 1/4 大）。
    /// </para>
    /// </summary>
    public static uint GetMipDimension(uint baseDimension, uint mipLevel)
    {
        if (baseDimension == 0)
            return 0;
        if (mipLevel == 0)
            return baseDimension;

        // 位移量超过 31 时直接用 1，避免 UB（C# 的 >> 会取模 32）。
        if (mipLevel >= 32)
            return 1;

        uint shifted = baseDimension >> (int)mipLevel;
        return shifted == 0 ? 1 : shifted;
    }

    /// <summary>
    /// 给定基础层尺寸，允许的最大 mip 层数（1 + floor(log2(最长边))）。
    /// <para>
    /// 例如 1024×1024 → 11 层，256×128 → 9 层，1×1 → 1 层。
    /// 申请超过这个层数会被后端拒绝。
    /// </para>
    /// </summary>
    public static uint GetMipLevelCount(uint width, uint height)
    {
        uint longest = Math.Max(width, height);
        if (longest == 0)
            return 1;

        uint levels = 1;
        while (longest > 1)
        {
            longest >>= 1;
            levels++;
        }

        return levels;
    }

    /// <summary>
    /// 由基础层数据逐级生成 mip 1..N，每级通过 <paramref name="upload"/> 回调上传。
    /// <para>
    /// 上一级的结果作为下一级的输入（不是反复从 mip0 采样），
    /// 这与 GPU 的行为一致，也是 box filter 能逐级收敛的前提。
    /// </para>
    /// <para>
    /// <b>非 2 的幂尺寸</b>：每级按 <c>max(1, w >> 1)</c> 缩小。
    /// 奇数边（如 25 → 12）会丢掉最后一行/列的一个像素 ——
    /// 这是 2×2 采样的固有限制，GPU 也这么做，不必特殊处理。
    /// </para>
    /// </summary>
    /// <param name="baseData">
    /// 第 0 层的紧密排列像素数据，长度必须等于
    /// <c>width * height * bytesPerPixel</c>。
    /// </param>
    /// <param name="width">第 0 层宽度（纹素）。</param>
    /// <param name="height">第 0 层高度（纹素）。</param>
    /// <param name="bytesPerPixel">每像素字节数，必须为 4。</param>
    /// <param name="mipLevels">要生成的总层数（含第 0 层）；&lt;= 1 时直接返回。</param>
    /// <param name="upload">
    /// 上传回调，参数为（该层数据, 层号, 该层宽, 该层高）。层号从 1 开始。
    /// </param>
    /// <exception cref="ArgumentException">
    /// 尺寸或数据长度不自洽，或 <paramref name="bytesPerPixel"/> 不是 4。
    /// </exception>
    public static void GenerateAndUpload(
        ReadOnlySpan<byte> baseData,
        uint width,
        uint height,
        int bytesPerPixel,
        uint mipLevels,
        MipUploadCallback upload
    )
    {
        if (bytesPerPixel != 4)
            throw new ArgumentException(
                $"MipmapGenerator only handles 4-byte pixel formats, got {bytesPerPixel}.",
                nameof(bytesPerPixel)
            );
        if (width == 0 || height == 0)
            throw new ArgumentException($"Base level size must be positive, got {width}x{height}.");
        if (upload is null)
            throw new ArgumentNullException(nameof(upload));
        if (mipLevels <= 1)
            return;

        // 层数不能超过尺寸允许的上限，否则后端会拒绝创建。
        uint maxLevels = GetMipLevelCount(width, height);
        uint target = Math.Min(mipLevels, maxLevels);
        if (target <= 1)
            return;

        ulong expected = (ulong)width * height * 4;
        if ((ulong)baseData.Length != expected)
            throw new ArgumentException(
                $"Base data length {baseData.Length} != {width}x{height}x4 = {expected}.",
                nameof(baseData)
            );

        // 上一层的源（第 0 层是调用方给的，之后逐级替换）
        byte[] previous = null;
        int prevW = (int)width;
        int prevH = (int)height;

        for (uint level = 1; level < target; level++)
        {
            int srcW = prevW;
            int srcH = prevH;
            int dstW = Math.Max(1, srcW >> 1);
            int dstH = Math.Max(1, srcH >> 1);

            var dst = new byte[dstW * dstH * 4];

            ReadOnlySpan<byte> src = level == 1 ? baseData : previous;

            DownsampleBox2x2(src, srcW, srcH, dst, dstW, dstH);

            upload(dst, level, (uint)dstW, (uint)dstH);

            previous = dst;
            prevW = dstW;
            prevH = dstH;
        }
    }

    /// <summary>
    /// 2×2 box filter 降采样，4 字节/像素。
    /// <para>
    /// 每像素取源图对应 2×2 块的平均值。源边为奇数时，最后一行/列
    /// 只取得到的一个像素（等价于复制边缘），避免越界读。
    /// </para>
    /// <para>
    /// 逐通道累加后一次除法。<b>不做逐字节的临时数组搬运</b>——
    /// 那样在 2048² 图上会慢一个数量级；这里的访问模式对 CPU 缓存友好
    /// （按行扫描，源与目标都是顺序访问）。
    /// </para>
    /// </summary>
    private static void DownsampleBox2x2(
        ReadOnlySpan<byte> src,
        int srcW,
        int srcH,
        byte[] dst,
        int dstW,
        int dstH
    )
    {
        for (int y = 0; y < dstH; y++)
        {
            int sy0 = y * 2;
            int sy1 = Math.Min(sy0 + 1, srcH - 1);

            int srcRow0 = sy0 * srcW * 4;
            int srcRow1 = sy1 * srcW * 4;
            int dstRow = y * dstW * 4;

            for (int x = 0; x < dstW; x++)
            {
                int sx0 = x * 2;
                int sx1 = Math.Min(sx0 + 1, srcW - 1);

                int o00 = srcRow0 + sx0 * 4;
                int o01 = srcRow0 + sx1 * 4;
                int o10 = srcRow1 + sx0 * 4;
                int o11 = srcRow1 + sx1 * 4;

                int d = dstRow + x * 4;

                // 四个像素逐通道求和后 /4。整数平均不引入浮点误差，
                // 且与 GPU 的 UNorm mip 生成结果一致。
                for (int c = 0; c < 4; c++)
                {
                    int sum = src[o00 + c] + src[o01 + c] + src[o10 + c] + src[o11 + c];
                    dst[d + c] = (byte)((sum + 2) >> 2);
                }
            }
        }
    }
}
