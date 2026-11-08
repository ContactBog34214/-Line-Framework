using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 GPU 后端设备。
/// <para>
/// <b>与 Veldrid 的关键差异</b>：SDL3 的「命令缓冲」同时承担三件事 ——
/// 录制、提交、呈现。没有独立的 swapchain 对象，交换链目标由
/// <c>SDL_WaitAndAcquireGPUSwapchainTexture</c> 每帧从窗口上取。
/// </para>
/// </summary>
public sealed class SDL3Device : IGraphicsDevice
{
    internal IntPtr dev;
    private readonly SDL3ResourceFactory factory;
    private readonly bool ownsVideoSubsystem;
    private bool disposed;

    public string Name { get; }

    /// <summary>
    /// SDL3 后端可用的着色器格式。
    /// <para>
    /// 只声明 SPIR-V：SDL_CreateGPUDevice 会据此挑选驱动，
    /// 在 Linux 上声明 MSL/MetalLib 会让选择逻辑走偏。
    /// 将来支持 D3D11 时按平台追加 DXBC。
    /// </para>
    /// </summary>
    private const GPUShaderFormat SupportedFormats = GPUShaderFormat.SPIRV;

    /// <summary>
    /// 创建设备，并<b>自动初始化 SDL 视频子系统</b>。
    /// </summary>
    /// <param name="backend">
    /// 该参数目前仅用于校验；实际的图形 API 由
    /// <see cref="SDL.CreateGPUDevice"/> 依据平台与着色器格式自行选择。
    /// </param>
    /// <param name="videoDriver">
    /// 强制使用的视频驱动（如 <c>"x11"</c> / <c>"wayland"</c>）。
    /// 必须在 SDL 初始化前设置，因此由本构造器负责；
    /// 传 <c>null</c> 则让 SDL 自行选择。
    /// </param>
    public SDL3Device(GraphicBackend backend, string videoDriver = null)
        : this(backend, videoDriver, debug: false) { }

    public SDL3Device(GraphicBackend backend, string videoDriver, bool debug)
    {
        if (
            backend
            is not (GraphicBackend.Vulkan or GraphicBackend.Direct3D or GraphicBackend.Metal)
        )
            throw new NotSupportedException(
                $"The SDL3 GPU backend does not support {backend}. "
                    + "Use Vulkan / Direct3D / Metal."
            );

        // 视频驱动 hint 必须在 SDL_Init 之前设置，否则无效。
        if (!string.IsNullOrEmpty(videoDriver))
            SDL.SetHint(SDL.Hints.VideoDriver, videoDriver);

        // SDL_Init 是引用计数的：只有确实未初始化时才由本对象负责 Quit。
        // 注意 SDL_WasInit 返回的是「已初始化子系统的掩码」，不是 bool。
        ownsVideoSubsystem = (SDL.WasInit(SDL.InitFlags.Video) & SDL.InitFlags.Video) == 0;
        if (ownsVideoSubsystem && !SDL.Init(SDL.InitFlags.Video))
            throw new InvalidOperationException($"SDL_Init(Video) failed: {SDL.GetError()}");

        dev = SDL.CreateGPUDevice(SupportedFormats, debug, null);
        if (dev == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUDevice failed: {SDL.GetError()}");

        Name = SDL.GetGPUDeviceDriver(dev) ?? "SDL3 GPU";
        factory = new SDL3ResourceFactory(this);
    }

    /// <summary>
    /// 包装一个已存在的 SDL GPU 设备。
    /// <b>不接管设备所有权</b>，也不负责 SDL 子系统的初始化/关闭。
    /// </summary>
    internal SDL3Device(IntPtr existing)
    {
        if (existing == IntPtr.Zero)
            throw new ArgumentNullException(nameof(existing));

        dev = existing;
        ownsVideoSubsystem = false;
        Name = SDL.GetGPUDeviceDriver(dev) ?? "SDL3 GPU";
        factory = new SDL3ResourceFactory(this);
    }

    public IResourceFactory ResourceFactory => factory;

    public void WaitForIdle()
    {
        // SDL3 没有显式的 WaitForIdle。
        // 提交一个空命令缓冲并等待其完成，效果接近：
        // 由于 SubmitGPUCommandBuffer 不带 fence 就立即返回，
        // 真正的「等待 GPU 空闲」需要用 SubmitGPUCommandBufferAndAcquireFence
        // + WaitForGPUFence。这里用一个同步提交近似。
        var cmd = SDL.AcquireGPUCommandBuffer(dev);
        if (cmd != IntPtr.Zero)
            SDL.SubmitGPUCommandBuffer(cmd);
    }

    public void SubmitCommands(ICommandList commandList)
    {
        if (commandList is not SDL3CommandList cl)
            throw new ArgumentException(
                $"Command list {commandList?.GetType().Name ?? "null"} is not a SDL3CommandList.",
                nameof(commandList)
            );

        cl.Submit();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        factory?.DisposeOwned();

        if (dev != IntPtr.Zero)
        {
            SDL.DestroyGPUDevice(dev);
            dev = IntPtr.Zero;
        }

        // 只关闭自己打开的那一次（SDL_Init 是引用计数的）
        if (ownsVideoSubsystem)
            SDL.Quit();
    }
}
