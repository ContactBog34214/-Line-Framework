namespace Line.Framework.Graphics;

/// <summary>
/// 像素格式的尺寸查询。属于「格式自身的固有属性」，与具体后端无关。
/// <para>
/// <b>为什么在核心库</b>：<see cref="Resource.Graphic.RResourceSet"/> 生成 mip 链时
/// 需要按格式算每级数据大小，而它在 Line.Framework 里。此前这些扩展方法在
/// Line.Framework.Default，核心库调不到 —— 搬上来是修正这一层位置错误。
/// </para>
/// <para>
/// 不放各后端的 Converter：那边只做 A 类型 → B 类型的映射，尺寸计算不是转换。
/// </para>
/// </summary>
public static class PixelFormatExtensions
{
    /// <summary>
    /// 单个像素占用的字节数（紧密排列，不含任何行对齐填充）。
    /// <para>
    /// 用途：
    /// 1. 上传纹理时校验数据长度是否为 width * height * bpp；
    /// 2. mip 链生成时逐级计算目标缓冲大小；
    /// 3. 读回纹理时计算目标缓冲大小。
    /// </para>
    /// </summary>
    public static int GetBytesPerPixel(this PixelFormat format)
    {
        return format switch
        {
            // 8-bit 单通道
            PixelFormat.R8UNorm or PixelFormat.R8SNorm or PixelFormat.R8UInt or PixelFormat.R8SInt =>
                1,

            // 8-bit 双通道
            PixelFormat.R8G8UNorm
            or PixelFormat.R8G8SNorm
            or PixelFormat.R8G8UInt
            or PixelFormat.R8G8SInt => 2,

            // 8-bit 四通道 —— UI 最常用的 B8G8R8A8UNorm 也在这里
            PixelFormat.R8G8B8A8UNorm
            or PixelFormat.B8G8R8A8UNorm
            or PixelFormat.R8G8B8A8SNorm
            or PixelFormat.R8G8B8A8UInt
            or PixelFormat.R8G8B8A8SInt => 4,

            // 16-bit 单通道
            PixelFormat.R16UNorm or PixelFormat.R16SNorm or PixelFormat.R16Float => 2,

            // 16-bit 双通道
            PixelFormat.R16G16UNorm or PixelFormat.R16G16SNorm or PixelFormat.R16G16Float => 4,

            // 16-bit 四通道
            PixelFormat.R16G16B16A16UNorm
            or PixelFormat.R16G16B16A16SNorm
            or PixelFormat.R16G16B16A16Float => 8,

            // 32-bit 单通道
            PixelFormat.R32Float => 4,

            // 32-bit 双通道
            PixelFormat.R32G32Float => 8,

            // 32-bit 四通道
            PixelFormat.R32G32B32A32Float => 16,

            // 深度格式：两个都是 32 位打包（24 位深度 + 8 位模板 也占 4 字节）
            PixelFormat.D24UNormS8UInt or PixelFormat.D32FloatS8UInt => 4,

            _ => throw new NotSupportedException($"Unsupported pixel format: {format}"),
        };
    }

    /// <summary>
    /// 指定区域的紧密排列字节总数。
    /// 返回 <see cref="ulong"/>：宽度/高度接近上限时乘积累加也不会溢出。
    /// </summary>
    public static ulong GetByteSize(this PixelFormat format, uint width, uint height, uint depth = 1)
    {
        // 先转 ulong，让整个乘法在 64 位下进行
        return (ulong)format.GetBytesPerPixel() * width * height * depth;
    }

    /// <summary>
    /// 整张纹理的紧密排列字节总数（按 mip 0 尺寸计算）。
    /// </summary>
    public static ulong GetByteSize(this PixelFormat format, ITexture texture)
    {
        if (texture is null)
            throw new ArgumentNullException(nameof(texture));

        return format.GetByteSize(texture.Width, texture.Height, texture.Depth);
    }
}
