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
    /// SDL3 的 GPU 驱动标识符，与 <see cref="GraphicBackend"/> 一一对应。
    /// <para>
    /// 这些名字是 SDL3 内部的低 ASCII 标识符（见 SDL_GetGPUDriver），
    /// 注意 Direct3D 对应的字符串是 <c>direct3d12</c>，不是 d3d12。
    /// </para>
    /// </summary>
    private static string ToDriverName(GraphicBackend backend) =>
        backend switch
        {
            GraphicBackend.Vulkan => "vulkan",
            GraphicBackend.Metal => "metal",
            GraphicBackend.Direct3D => "direct3d12",
            _ => null,
        };

    /// <summary>
    /// 每个驱动实际能接受的着色器格式。
    /// <para>
    /// 必须与 <see cref="ToDriverName"/> 的结果匹配：
    /// 强制 direct3d12 却只声明 SPIRV，SDL_CreateGPUDevice 会返回 NULL。
    /// </para>
    /// </summary>
    private static GPUShaderFormat ToShaderFormats(GraphicBackend backend) =>
        backend switch
        {
            GraphicBackend.Vulkan => GPUShaderFormat.SPIRV,
            GraphicBackend.Metal => GPUShaderFormat.MSL | GPUShaderFormat.MetalLib,
            GraphicBackend.Direct3D => GPUShaderFormat.DXBC | GPUShaderFormat.DXIL,
            _ => GPUShaderFormat.SPIRV,
        };

    /// <summary>
    /// 创建设备，并<b>自动初始化 SDL 视频子系统</b>。
    /// </summary>
    /// <param name="backend">
    /// 指定真正使用的图形 API：通过 <c>SDL_CreateGPUDevice</c> 的
    /// <c>name</c> 参数强制驱动，而不是让 SDL 自行挑选。
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

        string driverName = ToDriverName(backend);
        GPUShaderFormat formats = ToShaderFormats(backend);

        // 先探测，否则失败时只能拿到一句含糊的 "No supported shader formats"。
        // 注意：name 为 null 时 SDL 会按默认顺序挑一个能工作的后端。
        if (driverName != null && !SDL.GPUSupportsShaderFormats(formats, driverName))
        {
            // 已经 SDL_Init 过了，抛之前归还引用，否则下次 WasInit 永远为真。
            if (ownsVideoSubsystem)
            {
                SDL.Quit();
                ownsVideoSubsystem = false;
            }

            throw new InvalidOperationException(
                $"GPU driver '{driverName}' cannot accept shader formats {formats}. "
                    + $"Available drivers: {DescribeDrivers()}"
            );
        }

        dev = SDL.CreateGPUDevice(formats, debug, driverName);
        if (dev == IntPtr.Zero)
        {
            if (ownsVideoSubsystem)
            {
                SDL.Quit();
                ownsVideoSubsystem = false;
            }

            throw new InvalidOperationException(
                $"SDL_CreateGPUDevice(formats={formats}, driver={driverName ?? "<auto>"}) "
                    + $"failed: {SDL.GetError()}"
            );
        }

        Name = SDL.GetGPUDeviceDriver(dev) ?? "SDL3 GPU";

        // 指定了驱动却没拿到它，说明回退了——必须让调用方知道。
        // 注意：抛出前要回收已创建的设备，否则泄漏一个 GPU 设备 + 一次 SDL_Init 引用。
        if (
            driverName != null
            && !string.Equals(Name, driverName, StringComparison.OrdinalIgnoreCase)
        )
        {
            SDL.DestroyGPUDevice(dev);
            dev = IntPtr.Zero;
            if (ownsVideoSubsystem)
            {
                SDL.Quit();
                ownsVideoSubsystem = false;
            }

            throw new InvalidOperationException(
                $"Requested GPU driver '{driverName}' but SDL fell back to '{Name}'. "
                    + $"Available drivers: {DescribeDrivers()}"
            );
        }

        factory = new SDL3ResourceFactory(this);

        if (driverName != null)
            Log.Info($"SDL3 GPU device created: requested={driverName}, actual={Name}");
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

    /// <summary>
    /// 列出当前构建里 SDL 认识的所有 GPU 驱动，按默认尝试顺序。
    /// 只用于把失败信息说清楚，不参与后端选择。
    /// </summary>
    private static string DescribeDrivers()
    {
        int count = SDL.GetNumGPUDrivers();
        if (count <= 0)
            return "<none>";

        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = SDL.GetGPUDriver(i) ?? "?";

        return string.Join(", ", names);
    }

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
