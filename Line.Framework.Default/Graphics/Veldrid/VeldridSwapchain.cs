using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 交换链的包装。
/// <para>
/// 本身是「视图」而非「所有者」：底层的 <c>Veldrid.Swapchain</c> 由
/// <c>GraphicsDevice</c>（主交换链）或创建它的代码持有，
/// 因此 <see cref="Dispose"/> 不会释放底层对象。
/// </para>
/// </summary>
public sealed class VeldridSwapchain : ISwapchain
{
    internal readonly global::Veldrid.Swapchain swapchain;

    // 缓存包装对象，避免每帧 new。
    // 但底层 framebuffer 实例会在 Resize / present 后变化，
    // 所以必须比对引用，而不是只缓存一次。
    private global::Veldrid.Framebuffer cachedFramebuffer;
    private VeldridFrameBuffer cachedWrapper;

    public string Name => swapchain.Name ?? string.Empty;

    /// <summary>
    /// 当前帧的渲染目标。
    /// <para>
    /// 底层 swapchain image 由驱动轮转，<see cref="Resize"/> 之后
    /// Veldrid 会换一个新的 <c>Framebuffer</c> 实例。
    /// 本属性通过引用比对自动重建包装，但<b>调用方不应跨帧缓存返回值</b>。
    /// </para>
    /// <para>
    /// 返回的对象由交换链持有，<b>不要 Dispose</b>。
    /// </para>
    /// </summary>
    public IFrameBuffer FrameBuffer
    {
        get
        {
            var current = swapchain.Framebuffer;

            if (!ReferenceEquals(current, cachedFramebuffer))
            {
                cachedFramebuffer = current;
                // createInfo 留空：交换链的 framebuffer 没有创建描述，
                // 颜色附件列表会是空的。格式信息仍然正确（取自 framebuffer 自身）。
                cachedWrapper = new VeldridFrameBuffer(current);
            }

            return cachedWrapper;
        }
    }
    private readonly global::Veldrid.GraphicsDevice dev;

    internal VeldridSwapchain(global::Veldrid.Swapchain sc, global::Veldrid.GraphicsDevice gd)
    {
        swapchain = sc ?? throw new ArgumentNullException(nameof(sc));
        dev = gd;
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Swapchain size must be positive, got {width}x{height}."
            );

        swapchain.Resize((uint)width, (uint)height);
    }

    public void Dispose()
    {
        swapchain.Dispose();
    }

    public void Swap()
    {
        dev.SwapBuffers(swapchain);
    }
}

public partial class VeldridConverter
{
    /// <summary>
    /// 抽象层窗口句柄 → Veldrid 交换链源。
    /// <para>
    /// <b>注意参数顺序在平台之间并不一致</b>：
    /// <list type="bullet">
    /// <item>Win32 / Android：<c>(Handle, Display)</c> —— 句柄在前</item>
    /// <item>Xlib / Wayland：<c>(Display, Handle)</c> —— 显示连接在前</item>
    /// <item>Cocoa / MetalView：只有一个句柄</item>
    /// </list>
    /// 写反不会报错，只会得到一个无法呈现的 surface。
    /// </para>
    /// </summary>
    public static global::Veldrid.SwapchainSource ConvertSwapchainSource(SwapchainSource source)
    {
        if (source.Surface == nint.Zero)
            throw new ArgumentException(
                $"SwapchainSource.Handle is null (Kind = {source.Kind}).",
                nameof(source)
            );

        return source.Kind switch
        {
            // Windows: hwnd + hinstance
            NativeWindowKind.Win32 => global::Veldrid.SwapchainSource.CreateWin32(
                source.Surface,
                source.Connection
            ),

            // X11: display + window（注意顺序与 Win32 相反）
            NativeWindowKind.Xlib => global::Veldrid.SwapchainSource.CreateXlib(
                source.Connection,
                source.Surface
            ),

            // Wayland: display + surface（同上）
            NativeWindowKind.Wayland => global::Veldrid.SwapchainSource.CreateWayland(
                source.Connection,
                source.Surface
            ),

            // macOS: NSWindow*
            NativeWindowKind.Cocoa => global::Veldrid.SwapchainSource.CreateNSWindow(
                source.Surface
            ),

            // Android: ANativeWindow* + JNIEnv*
            NativeWindowKind.AndroidSurface => global::Veldrid.SwapchainSource.CreateAndroidSurface(
                source.Surface,
                source.Connection
            ),

            // iOS / Metal: NSView*
            NativeWindowKind.MetalView => global::Veldrid.SwapchainSource.CreateNSView(
                source.Surface
            ),

            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source.Kind,
                "Unsupported native window kind."
            ),
        };
    }

    /// <summary>
    /// Veldrid 交换链描述 → 抽象层创建信息。
    /// <para>
    /// 仅用于「读回一个已存在交换链的配置」，<b>不包含 <see cref="SwapchainSource"/></b> ——
    /// 见 <c>ConvertSwapchainSource(global::Veldrid.SwapchainSource)</c> 的说明。
    /// </para>
    /// </summary>
    public static SwapchainCreateInfo ConvertSwapchainDescription(
        global::Veldrid.SwapchainDescription des
    )
    {
        return new()
        {
            // Source 无法从 Veldrid 反查，留空由调用方自行填充
            Source = default,
            Width = des.Width,
            Height = des.Height,
            DepthFormat = des.DepthFormat.HasValue
                ? ConvertPixelFormat(des.DepthFormat.Value)
                : null,
            SyncToVerticalBlank = des.SyncToVerticalBlank,
            ColorSrgb = des.ColorSrgb,
        };
    }

    /// <summary>
    /// 抽象层创建信息 → Veldrid 交换链描述。
    /// </summary>
    public static global::Veldrid.SwapchainDescription ConvertSwapchainDescription(
        SwapchainCreateInfo createInfo
    )
    {
        if (createInfo.Width == 0 || createInfo.Height == 0)
            throw new ArgumentException(
                $"Swapchain size must be non-zero, got {createInfo.Width}x{createInfo.Height}.",
                nameof(createInfo)
            );

        return new global::Veldrid.SwapchainDescription(
            ConvertSwapchainSource(createInfo.Source),
            createInfo.Width,
            createInfo.Height,
            createInfo.DepthFormat.HasValue
                ? ConvertPixelFormat(createInfo.DepthFormat.Value)
                : null,
            createInfo.SyncToVerticalBlank
        )
        {
            ColorSrgb = createInfo.ColorSrgb,
        };
    }
}

public partial class VeldridResourceFactory
{
    public ISwapchain CreateSwapchain(SwapchainCreateInfo createInfo)
    {
        var sc = dev.ResourceFactory.CreateSwapchain(
            VeldridConverter.ConvertSwapchainDescription(createInfo)
        );
        return new VeldridSwapchain(sc, dev);
    }
}
