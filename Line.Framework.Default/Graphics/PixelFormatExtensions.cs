using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics;

/// <summary>
/// 像素格式的尺寸查询。属于「格式自身的固有属性」，与具体后端无关，
/// 因此放在后端共用的父命名空间里，Veldrid / SDL3 两个后端都能使用。
/// <para>
/// 不放 <c>VeldridConverter</c>：那边只做 A 类型 → B 类型的映射，
/// 尺寸计算不是转换。
/// </para>
/// <para>
/// 使用 <c>Framework.Graphics.PixelFormat</c> 全限定，避免与
/// <c>Veldrid.PixelFormat</c> / <c>SDL3.SDL.GPUTextureFormat</c> 混淆。
/// </para>
/// </summary>
public static class PixelFormatExtensions
{
    /// <summary>
    /// 单个像素占用的字节数（紧密排列，不含任何行对齐填充）。
    /// <para>
    /// 用途：
    /// 1. 上传纹理时校验数据长度是否为 width * height * bpp；
    /// 2. 读回纹理时计算目标缓冲大小。
    /// </para>
    /// </summary>
    public static int GetBytesPerPixel(this Framework.Graphics.PixelFormat format)
    {
        return format switch
        {
            // 8-bit 单通道
            Framework.Graphics.PixelFormat.R8UNorm
            or Framework.Graphics.PixelFormat.R8SNorm
            or Framework.Graphics.PixelFormat.R8UInt
            or Framework.Graphics.PixelFormat.R8SInt => 1,

            // 8-bit 双通道
            Framework.Graphics.PixelFormat.R8G8UNorm
            or Framework.Graphics.PixelFormat.R8G8SNorm
            or Framework.Graphics.PixelFormat.R8G8UInt
            or Framework.Graphics.PixelFormat.R8G8SInt => 2,

            // 8-bit 四通道 —— UI 最常用的 B8G8R8A8UNorm 也在这里
            Framework.Graphics.PixelFormat.R8G8B8A8UNorm
            or Framework.Graphics.PixelFormat.B8G8R8A8UNorm
            or Framework.Graphics.PixelFormat.R8G8B8A8SNorm
            or Framework.Graphics.PixelFormat.R8G8B8A8UInt
            or Framework.Graphics.PixelFormat.R8G8B8A8SInt => 4,

            // 16-bit 单通道
            Framework.Graphics.PixelFormat.R16UNorm
            or Framework.Graphics.PixelFormat.R16SNorm
            or Framework.Graphics.PixelFormat.R16Float => 2,

            // 16-bit 双通道
            Framework.Graphics.PixelFormat.R16G16UNorm
            or Framework.Graphics.PixelFormat.R16G16SNorm
            or Framework.Graphics.PixelFormat.R16G16Float => 4,

            // 16-bit 四通道
            Framework.Graphics.PixelFormat.R16G16B16A16UNorm
            or Framework.Graphics.PixelFormat.R16G16B16A16SNorm
            or Framework.Graphics.PixelFormat.R16G16B16A16Float => 8,

            // 32-bit 单通道
            Framework.Graphics.PixelFormat.R32Float => 4,

            // 32-bit 双通道
            Framework.Graphics.PixelFormat.R32G32Float => 8,

            // 32-bit 四通道
            Framework.Graphics.PixelFormat.R32G32B32A32Float => 16,

            _ => throw new NotSupportedException($"Unsupported pixel format: {format}"),
        };
    }

    /// <summary>
    /// 指定区域的紧密排列字节总数。
    /// 返回 <see cref="ulong"/>：宽度/高度接近上限时乘积累加也不会溢出。
    /// </summary>
    public static ulong GetByteSize(
        this Framework.Graphics.PixelFormat format,
        uint width,
        uint height,
        uint depth = 1
    )
    {
        // 先转 ulong，让整个乘法在 64 位下进行
        return (ulong)format.GetBytesPerPixel() * width * height * depth;
    }

    /// <summary>
    /// 整张纹理的紧密排列字节总数（按 mip 0 尺寸计算）。
    /// </summary>
    public static ulong GetByteSize(
        this Framework.Graphics.PixelFormat format,
        ITexture texture
    )
    {
        if (texture is null)
            throw new ArgumentNullException(nameof(texture));

        return format.GetByteSize(texture.Width, texture.Height, texture.Depth);
    }
}
