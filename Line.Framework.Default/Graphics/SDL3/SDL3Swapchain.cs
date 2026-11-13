using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 交换链的包装。
/// <para>
/// SDL3 没有独立的交换链对象：交换链目标每帧通过
/// <c>SDL_WaitAndAcquireGPUSwapchainTexture</c> 从窗口上取得。
/// 因此本类只是「窗口 + 已取到的纹理」的持有者。
/// </para>
/// <para>
/// <see cref="Swap"/> 是空操作 —— 呈现发生在
/// <c>IGraphicsDevice.SubmitCommands</c> 之中（提交即呈现）。
/// </para>
/// </summary>
public sealed class SDL3Swapchain : ISwapchain
{
    private readonly IntPtr window;
    private readonly SDL3FrameBuffer framebuffer;
    private readonly bool ownsClaim;

    public string Name { get; }

    internal SDL3Swapchain(SDL3Device device, IntPtr window, string name, bool ownsClaim)
    {
        this.window = window;
        this.ownsClaim = ownsClaim;
        Name = name ?? "SDL3Swapchain";

        // SDL_GPU 不接受带 SDL_WINDOW_TRANSPARENT 的窗口。
        // 这里先自己检查一次，好处是能报出「实际 flags 值」和「哪个 enum 位对上了」，
        // 而 SDL 的错误文案只说「不支持透明窗口」，看不出是标志位问题还是绑定错位。
        var flags = SDL.GetWindowFlags(window);
        const ulong TransparentBit = 0x40000000UL; // SDL_WINDOW_TRANSPARENT

        if (((ulong)flags & TransparentBit) != 0)
            throw new ArgumentException(
                $"Window has SDL_WINDOW_TRANSPARENT set (actual flags = 0x{(ulong)flags:X}).\n"
                    + "The SDL GPU API does not support transparent windows; do not pass "
                    + "WindowFlags.Transparent / Vulkan / Metal to CreateWindow.\n"
                    + $"Enum check: Vulkan = 0x{(ulong)WindowFlags.Vulkan:X}, "
                    + $"Metal = 0x{(ulong)WindowFlags.Metal:X}, "
                    + $"Transparent = 0x{(ulong)WindowFlags.Transparent:X} "
                    + "(native SDL3 values are 0x10000000 / 0x20000000 / 0x40000000)",
                nameof(window)
            );

        // 显示到窗口前必须先认领
        if (!SDL.ClaimWindowForGPUDevice(device.dev, window))
            throw new InvalidOperationException(
                $"SDL_ClaimWindowForGPUDevice failed: {SDL.GetError()} "
                    + $"(window flags = 0x{(ulong)SDL.GetWindowFlags(window):X})"
            );

        GPUPresentMode mode = GPUPresentMode.Immediate;
        if (!WindowSupportsGPUPresentMode(device.dev, window, GPUPresentMode.Immediate))
        {
            mode = GPUPresentMode.Mailbox;
            if (!WindowSupportsGPUPresentMode(device.dev, window, GPUPresentMode.Mailbox))
                mode = GPUPresentMode.VSync;
        }
        SetGPUSwapchainParameters(device.dev, window, GPUSwapchainComposition.SDR, mode);

        // 交换链格式由窗口决定，创建立即可以查询 —— 这就是管线的 Outputs 该用的格式。
        var swapFormat = SDL.GetGPUSwapchainTextureFormat(device.dev, window);
        var outputFormat = new OutputFormatDescription
        {
            ColorAttachments =
            [
                new OutputAttachmentDescription
                {
                    Format = SDL3Converter.ConvertPixelFormat(swapFormat),
                },
            ],
            DepthAttachment = null,
            SampleCount = SampleCount.Count1,
        };

        framebuffer = new SDL3FrameBuffer(window, name) { OutputFormat = outputFormat };
    }

    public IFrameBuffer FrameBuffer => framebuffer;

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Swapchain size must be positive, got {width}x{height}."
            );

        // SDL3 的交换链尺寸随窗口自动变化，
        // 这里只需让缓存的尺寸失效，下一次 SetFrameBuffer 会重新取值。
        framebuffer.Invalidate();
    }

    /// <summary>
    /// 空操作：SDL3 在提交命令缓冲时自动呈现。
    /// </summary>
    public void Swap()
    {
        // 有意为空。
    }

    public void Dispose()
    {
        if (ownsClaim && window != IntPtr.Zero)
        {
            // 注意：这里需要 device，但为了不让 swapchain 反向依赖设备，
            // 认领的释放留给设备销毁时统一处理（SDL 会清理）。
        }
    }
}

/// <summary>
/// SDL3 的渲染目标。
/// <para>
/// 交换链目标每帧由命令列表填充（<see cref="AcquiredTexture"/> 等）。
/// 只有「当前命令缓冲取到的那一个纹理」是有效的，
/// 不要跨帧持有，也不要 Dispose。
/// </para>
/// </summary>
public sealed class SDL3FrameBuffer : IFrameBuffer
{
    internal IntPtr Window { get; }

    /// <summary>本帧取到的交换链纹理。由 <c>SetFrameBuffer</c> 填充。</summary>
    internal IntPtr AcquiredTexture;
    internal uint AcquiredWidth;
    internal uint AcquiredHeight;

    /// <summary>上一次取到的尺寸，作为 <see cref="Width"/>/<see cref="Height"/> 的返回值。</summary>
    private uint width;
    private uint height;

    public string Name { get; }

    public uint Width => width;
    public uint Height => height;

    public OutputFormatDescription OutputFormat { get; internal set; }

    public IReadOnlyList<FramebufferAttachmentDescription> ColorTargets =>
        Array.Empty<FramebufferAttachmentDescription>();

    public FramebufferAttachmentDescription? DepthTarget => null;

    internal SDL3FrameBuffer(IntPtr window, string name)
    {
        Window = window;
        Name = name ?? "SDL3SwapchainTarget";
    }

    internal void Invalidate()
    {
        width = 0;
        height = 0;
    }

    internal void ApplyAcquiredSize()
    {
        width = AcquiredWidth;
        height = AcquiredHeight;
    }

    public void Dispose()
    {
        // 交换链纹理由实现管理，不能释放。
    }
}
