using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridDevice : IGraphicsDevice
{
    private readonly GraphicsDevice graphicsDevice;
    public string Name => graphicsDevice.DeviceName;

    public VeldridDevice(GraphicBackend backend)
    {
        GraphicsDeviceOptions options = new()
        {
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = false,
            ResourceBindingModel = ResourceBindingModel.Default,
        };
        switch (backend)
        {
            case GraphicBackend.Vulkan:
                graphicsDevice = GraphicsDevice.CreateVulkan(options);
                break;
            case GraphicBackend.Metal:
                graphicsDevice = GraphicsDevice.CreateMetal(options);
                break;
            case GraphicBackend.Direct3D:
                graphicsDevice = GraphicsDevice.CreateD3D11(options);
                break;
            case GraphicBackend.OpenGL:
                throw new NotSupportedException();
        }
        ResourceFactory = new VeldridResourceFactory(graphicsDevice);
    }

    public IResourceFactory ResourceFactory { get; }

    public void Dispose()
    {
        // 先释放工厂持有的常驻资源（默认采样器等），再释放设备
        (ResourceFactory as VeldridResourceFactory)?.DisposeOwned();
        graphicsDevice?.Dispose();
    }

    public void WaitForIdle()
    {
        graphicsDevice?.WaitForIdle();
    }

    public void SubmitCommands(ICommandList commandList)
    {
        if (commandList is not VeldridCommandList)
            throw new InvalidDataException();
        graphicsDevice?.SubmitCommands((commandList as VeldridCommandList).cl);
    }
}

/// <summary>
/// 像素格式的尺寸查询。属于「格式自身的固有属性」，与后端无关，
/// 因此不放在 VeldridConverter（那边只做 A 类型 → B 类型的映射）。
/// <para>
/// 全部使用 <c>Framework.Graphics.PixelFormat</c> 全限定：
/// 本命名空间同时 using 了 Veldrid，裸写 PixelFormat 会与
/// <c>Veldrid.PixelFormat</c> 产生 CS0104 歧义。
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
    public static ulong GetByteSize(this Framework.Graphics.PixelFormat format, ITexture texture)
    {
        if (texture is null)
            throw new ArgumentNullException(nameof(texture));

        return format.GetByteSize(texture.Width, texture.Height, texture.Depth);
    }
}
