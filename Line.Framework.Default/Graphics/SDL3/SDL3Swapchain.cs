using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;
// SDL3.SDL 里也有一个 PixelFormat（纹理格式的旧枚举），
// 与抽象层的同名类型冲突，必须显式指向抽象层那个。
using PixelFormat = Line.Framework.Graphics.PixelFormat;

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
    private readonly SDL3Device device;
    private readonly IntPtr window;
    private readonly SDL3SwapchainTarget renderTarget;
    private readonly bool ownsClaim;

    /// <summary>请求的深度格式；null = 不带深度附件。</summary>
    private readonly PixelFormat? depthFormat;

    /// <summary>
    /// 交换链的深度纹理。
    /// <para>
    /// SDL 只提供颜色纹理，深度必须自己建。它的尺寸必须与
    /// acquire 到的交换链纹理<b>完全一致</b>，因此真实尺寸要等到
    /// <c>SDL_WaitAndAcquireGPUSwapchainTexture</c> 之后才知道 ——
    /// 见 <see cref="EnsureDepthTexture"/>。
    /// </para>
    /// </summary>
    private ITexture depthTexture;

    private uint depthWidth;
    private uint depthHeight;

    /// <summary>期望的垂直同步状态。</summary>
    private bool vsync;

    /// <summary>
    /// 期望的 present mode 与「已应用」的可能不同步：SDL 要求
    /// <c>SDL_SetGPUSwapchainParameters</c> 早于取交换链纹理，因此只能标脏，
    /// 由下一次 <see cref="ApplyPendingPresentMode"/> 落地。
    /// </summary>
    private bool presentModeDirty;

    public string Name { get; }

    internal SDL3Swapchain(
        SDL3Device device,
        IntPtr window,
        string name,
        bool ownsClaim,
        PixelFormat? depthFormat,
        bool syncToVerticalBlank
    )
    {
        this.device = device;
        this.window = window;
        this.ownsClaim = ownsClaim;
        this.depthFormat = depthFormat;
        this.vsync = syncToVerticalBlank;
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

        // present mode 按偏好挑（此前固定 Immediate → Mailbox → VSync，
        // 忽略了 SwapchainCreateInfo.SyncToVerticalBlank —— 那是个死字段）。
        ApplyPresentMode(device.dev, window);

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
            // 深度格式在这里就确定下来，而不是等纹理建好才填：
            // 管线创建只读这个值（DepthStencilFormat 由它决定），
            // 而管线通常在 acquire 之前就建好了。
            DepthAttachment = depthFormat.HasValue
                ? new OutputAttachmentDescription { Format = depthFormat.Value }
                : null,
            SampleCount = SampleCount.Count1,
        };

        renderTarget = new SDL3SwapchainTarget(this, window, name) { OutputFormat = outputFormat };
    }

    /// <summary>当前深度纹理；由 <see cref="EnsureDepthTexture"/> 维护。</summary>
    internal ITexture CurrentDepthTexture => depthTexture;

    public IRenderTarget RenderTarget => renderTarget;

    /// <summary>
    /// 垂直同步。SDL3 是窗口级开关，通过 present mode 实现。
    /// <para>
    /// 设值只标脏 —— SDL 要求 <c>SDL_SetGPUSwapchainParameters</c> 早于
    /// acquire 交换链纹理，真正的切换发生在下一次取纹理之前
    /// （见 <see cref="ApplyPendingPresentMode"/>）。
    /// </para>
    /// </summary>
    public bool SyncToVerticalBlank
    {
        get => vsync;
        set
        {
            if (vsync == value)
                return;

            vsync = value;
            presentModeDirty = true;
        }
    }

    /// <summary>
    /// 按 vsync 偏好挑一个当前窗口<b>真实支持</b>的 present mode。
    /// <para>
    /// 部分合成器不支持 Mailbox / Immediate，此时
    /// <c>SDL_WindowSupportsGPUPresentMode</c> 会明确返回 false，
    /// 所以这里逐个降级而不是硬设。
    /// </para>
    /// </summary>
    private static GPUPresentMode ChoosePresentMode(
        SDL3Device device,
        IntPtr window,
        bool useVSync
    )
    {
        ReadOnlySpan<GPUPresentMode> preference = useVSync
            ? [GPUPresentMode.VSync, GPUPresentMode.Mailbox, GPUPresentMode.Immediate]
            : [GPUPresentMode.Immediate, GPUPresentMode.Mailbox, GPUPresentMode.VSync];

        foreach (var mode in preference)
            if (WindowSupportsGPUPresentMode(device.dev, window, mode))
                return mode;

        Log.Warning("[SDL3] no supported GPU present mode detected; falling back to VSync.");
        return GPUPresentMode.VSync;
    }

    private void ApplyPresentMode(IntPtr deviceHandle, IntPtr windowHandle)
    {
        var mode = ChoosePresentMode(device, windowHandle, vsync);

        if (
            !SetGPUSwapchainParameters(
                deviceHandle,
                windowHandle,
                GPUSwapchainComposition.SDR,
                mode
            )
        )
            // 不抛：合成器不支持某个 mode 是环境限制，保留旧 mode 继续跑。
            Log.Warning(
                $"[SDL3] SDL_SetGPUSwapchainParameters({mode}) failed: {SDL.GetError()}. "
                    + "Keeping the previous present mode."
            );

        presentModeDirty = false;
    }

    /// <summary>
    /// 落地待应用的 present mode。
    /// <para>
    /// <b>必须在 acquire 交换链纹理之前调用</b> —— 否则 SDL 会拒绝本次设置，
    /// 表现为「设了 vsync 但毫无变化」。
    /// </para>
    /// </summary>
    internal void ApplyPendingPresentMode()
    {
        if (!presentModeDirty)
            return;

        ApplyPresentMode(device.dev, window);
    }

    /// <summary>
    /// 确保深度纹理存在且尺寸与本次 acquire 到的交换链纹理一致，返回其句柄。
    /// <para>
    /// <b>必须在每次 acquire 之后调用</b>：交换链纹理的真实尺寸只有 acquire
    /// 才知道（窗口缩放与 present 时序可能错开），而渲染通道要求深度附件
    /// 与颜色附件尺寸完全相同，否则 Vulkan 会截断或报验证错误。
    /// </para>
    /// <para>
    /// 没有请求深度时返回 <see cref="IntPtr.Zero"/>。
    /// </para>
    /// </summary>
    internal IntPtr EnsureDepthTexture(uint width, uint height)
    {
        if (depthFormat is not { } format)
            return IntPtr.Zero;

        if (width == 0 || height == 0)
            return IntPtr.Zero;

        if (depthTexture is not null && depthWidth == width && depthHeight == height)
            return ((SDL3Texture)depthTexture).handle;

        // 尺寸变了（首次创建 / 窗口缩放）→ 重建。
        // SDL_ReleaseGPUTexture 是延迟释放的：GPU 仍在引用时 SDL 会在
        // 合适时机真正回收，因此这里不需要先 WaitForIdle。
        depthTexture?.Dispose();
        depthTexture = null;

        depthTexture = device.ResourceFactory.CreateTexture(
            new TextureCreateInfo
            {
                Width = width,
                Height = height,
                Format = format,
                Usage = TextureUsage.DepthStencilTarget,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
            }
        );

        depthWidth = width;
        depthHeight = height;
        return ((SDL3Texture)depthTexture).handle;
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Swapchain size must be positive, got {width}x{height}."
            );

        // SDL3 的交换链尺寸随窗口自动变化，
        // 这里只需让缓存的尺寸失效，下一次 SetRenderTarget 会重新取值。
        renderTarget.Invalidate();
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
        // 深度纹理是我们自己建的，必须释放 —— 否则反复开关窗口会持续泄漏。
        // 交换链的颜色纹理由 SDL 管理，不能碰。
        depthTexture?.Dispose();
        depthTexture = null;
        depthWidth = 0;
        depthHeight = 0;

        if (ownsClaim && window != IntPtr.Zero)
        {
            // 认领的释放留给设备销毁时统一处理（SDL 会清理），
            // 避免 swapchain 在设备之后被销毁时访问已释放的设备。
        }
    }
}

/// <summary>
/// SDL3 的交换链渲染目标。
/// <para>
/// 与 <see cref="SDL3RenderTarget"/> 的区别：这里的纹理不是本对象创建的，
/// 而是每帧通过 <c>SDL_WaitAndAcquireGPUSwapchainTexture</c> 从窗口取得，
/// 只能由取得它的那个命令缓冲引用，且只在本帧有效。
/// </para>
/// <para>
/// 因此 <see cref="ColorTextures"/> 返回空列表：交换链纹理不可采样
/// （Vulkan 的 present layout 不支持，SDL3 也只是每帧一个临时句柄）。
/// </para>
/// </summary>
public sealed class SDL3SwapchainTarget : IRenderTarget
{
    /// <summary>
    /// 持有它的交换链。
    /// <para>
    /// 深度纹理由交换链创建与管理（它的生命周期跨越「交换链重建尺寸」），
    /// 目标只是借用。循环引用是安全的：两者同生共死。
    /// </para>
    /// </summary>
    private readonly SDL3Swapchain owner;

    internal IntPtr Window { get; }

    /// <summary>上一次取到的尺寸，作为 <see cref="Width"/>/<see cref="Height"/> 的返回值。</summary>
    private uint width;
    private uint height;

    public string Name { get; }

    public uint Width => width;
    public uint Height => height;

    public OutputFormatDescription OutputFormat { get; internal set; }

    public IReadOnlyList<ITexture> ColorTextures => [];

    /// <summary>
    /// 深度纹理；没有请求深度时为 null。
    /// <para>
    /// 由交换链拥有，<b>不要 Dispose</b>。尺寸在每次 acquire 后可能与
    /// <see cref="Width"/>/<see cref="Height"/> 同步变化。
    /// </para>
    /// </summary>
    public ITexture DepthTexture => owner.CurrentDepthTexture;

    internal SDL3SwapchainTarget(SDL3Swapchain owner, IntPtr window, string name)
    {
        this.owner = owner;
        Window = window;
        Name = name ?? "SDL3SwapchainTarget";
    }

    internal void Invalidate()
    {
        width = 0;
        height = 0;
    }

    internal void ApplyAcquiredSize(uint w, uint h)
    {
        width = w;
        height = h;
    }

    /// <summary>见 <see cref="SDL3Swapchain.EnsureDepthTexture"/>。</summary>
    internal IntPtr EnsureDepthTexture(uint w, uint h) => owner.EnsureDepthTexture(w, h);

    /// <summary>见 <see cref="SDL3Swapchain.ApplyPendingPresentMode"/>。</summary>
    internal void ApplyPendingPresentMode() => owner.ApplyPendingPresentMode();

    public void Dispose()
    {
        // 交换链纹理由 SDL 管理、深度纹理由交换链管理，这里都不能释放。
    }
}
