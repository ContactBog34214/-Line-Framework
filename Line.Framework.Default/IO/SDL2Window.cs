using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.IO.Events;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using S2 = SDL2.SDL;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL2 的 <see cref="IWindow"/> 实现。
/// <para>
/// <b>与 <see cref="SDL3Window"/> 的结构对齐</b>（队列 + 缓冲 + 事件泵委托），
/// 差异全部集中在 SDL2 自己的 API 形状上：全 <c>SDL_</c> 前缀、
/// 窗口是裸 <c>IntPtr</c>、事件是统一 union + 子类型。
/// </para>
/// <para>
/// <b>本类不轮询、不跑循环</b>：事件由 <see cref="Sdl2EventPump"/> 泵出后按
/// 窗口 ID 投递到各自的队列。
/// </para>
/// <para>
/// ⚠️ <b>当前不能挂到 <c>WindowType</c> 上</b>：那边的 <c>EventPool</c> 以
/// <c>SDL3.SDL.EventType</c> 为键、<c>ToSdlEvent</c> 直接强转 <c>SDL3.SDL.Event</c>。
/// SDL2 事件进去会得到 <c>default</c>，被静默丢弃。等 core 有了自己的事件类型
/// （或 App 接管事件分发）才能接入。
/// </para>
/// </summary>
public sealed class SDL2Window : ISdl2Window
{
    private bool ownsWindow;
    private nint window;
    private SwapchainSource source;
    private bool disposed;

    /// <summary>已设置的图标 surface（SDL2 的图标由我们持有并负责释放）。</summary>
    private nint iconSurface;

    /// <summary>像素尺寸退化只报一次。</summary>
    private bool pixelSizeDegradeReported;

    // ------------------------------------------------------------------ 事件

    /// <summary>
    /// 新到的事件。<b>存的是已转换的抽象层事件</b> —— 原生事件里的文本
    /// 指向 SDL 内部缓冲区，下一次泵事件就失效，所以必须在入队时转换完。
    /// </summary>
    private readonly ConcurrentQueue<IWindowEvent> eventsQueue = new();

    private readonly CircularBuffer<IWindowEvent> eventsBuffer;

    /// <summary>事件容量，队列与暂存缓冲共用。构造期定，运行期不改。</summary>
    public int EventCapacity { get; }

    public const int DefaultEventCapacity = 48;

    // ------------------------------------------------------------------ 构造

    public SDL2Window(WindowCreateInfo info, int eventCapacity = DefaultEventCapacity)
    {
        info = info.WithSanitizedSize();
        EventCapacity = eventCapacity > 0 ? eventCapacity : DefaultEventCapacity;
        eventsBuffer = new(EventCapacity);
        Initialize(info.Title, info.Width, info.Height, info.Flags);
    }

    public SDL2Window(
        string title = "Title",
        int width = 640,
        int height = 480,
        WindowFlags flags = WindowFlags.Resizable | WindowFlags.InputFocus | WindowFlags.MouseFocus,
        int eventCapacity = DefaultEventCapacity
    )
    {
        EventCapacity = eventCapacity > 0 ? eventCapacity : DefaultEventCapacity;
        eventsBuffer = new(EventCapacity);
        Initialize(title, width, height, flags);
    }

    private void Initialize(string title, int width, int height, WindowFlags flags)
    {
        if (width <= 0)
            width = 640;
        if (height <= 0)
            height = 480;
        if (string.IsNullOrEmpty(title))
            title = "Title";

        // SDL_Init 是引用计数的：只确保视频子系统可用，不负责 Quit
        //（销毁窗口不等于关子系统）。
        if (S2.SDL_InitSubSystem(S2.SDL_INIT_VIDEO) != 0)
            throw new InvalidOperationException($"SDL_InitSubSystem failed: {S2.SDL_GetError()}");

        // SDL2 的窗口位置必须显式给：SDL_WINDOWPOS_CENTERED。
        var handle = S2.SDL_CreateWindow(
            title,
            S2.SDL_WINDOWPOS_CENTERED,
            S2.SDL_WINDOWPOS_CENTERED,
            width,
            height,
            SDL2Converter.ConvertWindowFlags(flags)
        );

        if (handle == nint.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {S2.SDL_GetError()}");

        window = handle;
        ownsWindow = true;
        Id = S2.SDL_GetWindowID(handle);
        source = BuildSource(handle);

        Attributes.Changing += OnAttributeChanging;
        Sdl2EventPump.Register(this);
    }

    // ------------------------------------------------------------------ IWindow 基本状态

    public string Name => Title;

    public nint NativeHandle => window;

    public nint SdlWindow => window;

    public uint Id { get; private set; }

    public WindowAttributes Attributes { get; } = new();

    public SwapchainSource Source => source;

    public event Action Resized;
    public event Action Moved;
    public event Action FocusGained;
    public event Action FocusLost;
    public event Action CloseRequested;

    public Vector2 Size
    {
        get
        {
            S2.SDL_GetWindowSize(window, out int w, out int h);
            return new(w, h);
        }
        set => S2.SDL_SetWindowSize(window, (int)value.X, (int)value.Y);
    }

    /// <summary>
    /// 像素尺寸。
    /// <para>
    /// <b>SDL2 没有通用 API</b>：<c>SDL_GL_GetDrawableSize</c> /
    /// <c>SDL_Vulkan_GetDrawableSize</c> 都要求先有上下文。这里按
    /// 「显示 DPI / 96」反推，拿不到就退回 <see cref="Size"/>。
    /// </para>
    /// </summary>
    public Vector2 PixelSize
    {
        get
        {
            // SDL2 没有 SDL_GetWindowSizeInPixels。
            // 想拿真实像素尺寸只能：
            //   1) SDL_GL_GetDrawableSize / SDL_Vulkan_GetDrawableSize ——
            //      必须先有 GL 或 Vulkan 上下文（本框架用 Veldrid，窗口建好时还没有）；
            //   2) SDL_GetWindowDisplayIndex + SDL_GetDisplayDPI 反推 ——
            //      这两个函数的签名在 ppy.SDL2-CS 1.0.82 里未核对。
            //
            // 所以这里退化为逻辑尺寸：明确给一个近似值，好过给一个可能错的「精确值」。
            if (!pixelSizeDegradeReported)
            {
                pixelSizeDegradeReported = true;
                Log.Debug(
                    "[SDL2Window] PixelSize falls back to the logical size. "
                        + "SDL2 cannot query drawable size without a GL/Vulkan context; "
                        + "on HiDPI displays the swapchain may be sized wrong."
                );
            }

            return Size;
        }
    }

    public Vector2 Position
    {
        get
        {
            S2.SDL_GetWindowPosition(window, out int x, out int y);
            return new(x, y);
        }
        set => S2.SDL_SetWindowPosition(window, (int)value.X, (int)value.Y);
    }

    public string Title
    {
        get => S2.SDL_GetWindowTitle(window) ?? string.Empty;
        set => S2.SDL_SetWindowTitle(window, value);
    }

    public bool Exists => window != nint.Zero && S2.SDL_GetWindowID(window) != 0;

    public bool IsFocus => S2.SDL_GetKeyboardFocus() == window;

    public bool IsMouseFocus => S2.SDL_GetMouseFocus() == window;

    public bool Visible
    {
        get =>
            ((uint)S2.SDL_GetWindowFlags(window) & (uint)S2.SDL_WindowFlags.SDL_WINDOW_HIDDEN) == 0;
        set
        {
            if (value)
                S2.SDL_ShowWindow(window);
            else
                S2.SDL_HideWindow(window);
        }
    }

    public bool FullScreen
    {
        get
        {
            const uint mask =
                (uint)S2.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN
                | (uint)S2.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;

            return ((uint)S2.SDL_GetWindowFlags(window) & mask) != 0;
        }
        set
        {
            // 统一用无边框全屏：不改显示模式，副作用最小。
            var flags = value ? S2.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP : 0;

            S2.SDL_SetWindowFullscreen(window, (uint)flags);
            ReapplyInputState();
        }
    }

    // ------------------------------------------------------------------ Source

    /// <summary>
    /// 填 <see cref="SwapchainSource"/>。
    /// <para>
    /// 走 <c>SDL_GetWindowWMInfo</c> + <c>SDL_SysWMinfo.info</c> 这个平台 union。
    /// 字段名按 <c>ppy.SDL2-CS</c> 的 <c>strings</c> 结果核对：
    /// <c>winrt</c> / <c>cocoa</c> / <c>android</c> / <c>hinstance</c> / <c>display</c>
    /// 都在，而 <c>x11</c> / <c>win</c> / <c>wl</c> 只是因 <c>strings</c> 默认最短长度
    /// 4 才没显示出来（它们是 3 / 3 / 2 个字符）。
    /// </para>
    /// <para>
    /// 若编译报「某字段不存在」，用
    /// <c>strings -n 2 SDL2-CS.dll | grep -E "^(x11|win|wl)$"</c> 复核。
    /// </para>
    /// </summary>
    private static SwapchainSource BuildSource(nint handle)
    {
        var source = new SwapchainSource();
        var driver = S2.SDL_GetCurrentVideoDriver() ?? string.Empty;

        try
        {
            var info = new S2.SDL_SysWMinfo();

            // 手动填版本号，不调 SDL_VERSION() —— 那个辅助函数的签名
            //（ref / out）在 ppy.SDL2-CS 里没核对过，而直接写字段是稳的。
            // SDL_GetWindowWMInfo 主要校验 major，minor/patch 给个合理值即可。
            info.version.major = 2;
            info.version.minor = 0;
            info.version.patch = 20;

            if (S2.SDL_GetWindowWMInfo(handle, ref info) != S2.SDL_bool.SDL_TRUE)
            {
                Log.Warning(
                    $"[SDL2Window] SDL_GetWindowWMInfo failed ({S2.SDL_GetError()}); "
                        + "SwapchainSource left empty."
                );
                return source;
            }

            switch (driver)
            {
                case "x11":
                    source.Surface = info.info.x11.window;
                    source.Connection = info.info.x11.display;
                    source.Kind = NativeWindowKind.Xlib;
                    break;

                case "wayland":
                    source.Surface = info.info.wl.surface;
                    source.Connection = info.info.wl.display;
                    source.Kind = NativeWindowKind.Wayland;
                    break;

                case "windows":
                    source.Surface = info.info.win.window;
                    source.Connection = info.info.win.hinstance;
                    source.Kind = NativeWindowKind.Win32;
                    break;

                case "cocoa":
                    source.Surface = info.info.cocoa.window;
                    source.Kind = NativeWindowKind.Cocoa;
                    break;

                default:
                    Log.Warning(
                        $"[SDL2Window] unsupported video driver '{driver}'; "
                            + "SwapchainSource left empty."
                    );
                    return source;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SDL2Window] building SwapchainSource failed: {ex.Message}");
            return source;
        }

        if (source.Surface == nint.Zero)
            Log.Warning(
                $"[SDL2Window] native handle for driver '{driver}' is null; "
                    + "render backends will not be able to present to this window."
            );

        return source;
    }

    /// <summary>
    /// 本地单调时钟，作为事件时间的临时来源。
    /// <para>
    /// 与 <see cref="SDL2EventConverter"/> 用的是同一套语义（都是本地时钟），
    /// 只是各自持有实例 —— 合并它需要多引一层，暂不值得。
    /// </para>
    /// </summary>
    private static readonly System.Diagnostics.Stopwatch EventClock =
        System.Diagnostics.Stopwatch.StartNew();

    // ------------------------------------------------------------------ 事件

    public void PumpEvents()
    {
        if (disposed)
            return;

        Sdl2EventPump.Pump(force: false);
    }

    /// <summary>强制泵一次，跳过「本帧已泵过」的判断。</summary>
    public void PumpEvents(bool force)
    {
        if (disposed)
            return;

        Sdl2EventPump.Pump(force);
    }

    /// <summary>
    /// 取走第一个匹配的事件。顺序：缓冲（旧）→ 队列（新），命中即停。
    /// </summary>
    public bool PollEvent(out IWindowEvent evt, Func<IWindowEvent, bool> filter = null)
    {
        var count = eventsBuffer.Count;
        for (long i = 0; i < count; i++)
        {
            var candidate = eventsBuffer[i];

            if (filter == null || filter(candidate))
            {
                eventsBuffer.RemoveAt(i);
                evt = candidate;
                return true;
            }
        }

        while (eventsQueue.TryDequeue(out var candidate))
        {
            if (filter == null || filter(candidate))
            {
                evt = candidate;
                return true;
            }

            eventsBuffer.Add(candidate);
        }

        evt = null;
        return false;
    }

    /// <summary>事件泵投递入口（主线程调用，原生事件此时仍然有效）。</summary>
    internal unsafe void Enqueue(S2.SDL_Event evt)
    {
        // 窗口状态事件（尺寸 / 焦点 / 关闭 / 全屏…）直接触发 IWindow 的 event，
        // 不进队列 —— 它们是「最新值」语义；队列只承载「序列」语义的输入。
        if (TryHandleWindowEvent(evt))
            return;

        Enqueue(SDL2EventConverter.Convert(this, evt));
    }

    /// <summary>
    /// 处理 SDL2 的窗口状态事件。
    /// <para>
    /// <b>SDL2 与 SDL3 的关键差异</b>：所有窗口状态都归为
    /// <c>SDL_WINDOWEVENT</c> 一个顶层类型，具体是哪种要看
    /// <c>window.windowEvent</c> 子字段。
    /// </para>
    /// </summary>
    /// <returns>true 表示已处理，不要再入队。</returns>
    private bool TryHandleWindowEvent(S2.SDL_Event evt)
    {
        if ((S2.SDL_EventType)evt.type != S2.SDL_EventType.SDL_WINDOWEVENT)
            return false;

        switch ((S2.SDL_WindowEventID)evt.window.windowEvent)
        {
            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                Resized?.Invoke();
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
                Moved?.Invoke();
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                Raise();
                ApplyMouseCapture(Attributes.RelativeMouse);
                FocusGained?.Invoke();
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                S2.SDL_SetRelativeMouseMode(S2.SDL_bool.SDL_FALSE);
                FocusLost?.Invoke();
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
                ApplyMouseCapture(Attributes.RelativeMouse);
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                ApplyMouseCapture(false);
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED:
                Raise();
                ReapplyInputState();
                return true;

            case S2.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                CloseRequested?.Invoke();
                Enqueue(
                    new WindowCloseEvent(this, EventClock.Elapsed)
                );
                return true;
        }

        return false;
    }

    /// <summary>事件泵投递「无归属窗口的全局事件」。</summary>
    internal void EnqueueGlobal(S2.SDL_Event evt) =>
        Enqueue(
            new UnknownEvent(
                this,
                EventClock.Elapsed,
                evt,
                isGlobal: true
            )
        );

    private void Enqueue(IWindowEvent item)
    {
        // 有界：满了丢最旧的。同时防止「PollEvent 长期从 buffer 命中导致
        // queue 无限增长」。
        while (eventsQueue.Count >= EventCapacity && eventsQueue.TryDequeue(out _)) { }

        eventsQueue.Enqueue(item);
    }

    /// <summary>取出原生事件（仅 <see cref="UnknownEvent"/> 有）。</summary>
    public S2.SDL_Event ToSdl2Event(UnknownEvent evt) => (S2.SDL_Event)evt.Native;

    // ------------------------------------------------------------------ 其它

    public void Raise()
    {
        if (window == nint.Zero)
            return;

        S2.SDL_RaiseWindow(window);
        S2.SDL_ShowWindow(window);
    }

    /// <summary>
    /// 立即抓取 / 释放鼠标。
    /// <para>
    /// <b>SDL2 只有进程级相对模式</b>：无焦点时直接返回，避免把焦点窗口的
    /// 抓取状态改掉。这条是 <see cref="IWindow.ApplyMouseCapture"/> 文档里
    /// 承诺的多窗口语义在 SDL2 上的落地方式。
    /// </para>
    /// </summary>
    public void ApplyMouseCapture(bool capture)
    {
        if (window == nint.Zero)
            return;

        if (!IsFocus)
            return;

        S2.SDL_SetRelativeMouseMode(capture ? S2.SDL_bool.SDL_TRUE : S2.SDL_bool.SDL_FALSE);

        ApplyCursorState(capture);
    }

    private void ApplyCursorState(bool? pendingRelative = null)
    {
        var relative = pendingRelative ?? (S2.SDL_GetRelativeMouseMode() == S2.SDL_bool.SDL_TRUE);

        if (Attributes.ShowCursor && !relative)
            S2.SDL_ShowCursor(
                1 /* SDL_ENABLE */
            );
        else
            S2.SDL_ShowCursor(
                0 /* SDL_DISABLE */
            );
    }

    /// <summary>把「用户意图」重新落地（平台操作重置了输入状态之后调用）。</summary>
    internal void ReapplyInputState()
    {
        if (window == nint.Zero)
            return;

        if (!IsFocus)
            return;

        S2.SDL_SetRelativeMouseMode(
            Attributes.RelativeMouse ? S2.SDL_bool.SDL_TRUE : S2.SDL_bool.SDL_FALSE
        );
        ApplyCursorState(Attributes.RelativeMouse);
    }

    public void RequestClose() => CloseRequested?.Invoke();

    /// <summary>
    /// 从 LTX v1 流设置图标（与 <see cref="SDL3Window.SetIcon"/> 同款格式）。
    /// </summary>
    public void SetIcon(Stream iconStream, bool closeStream = false)
    {
        if (iconStream is null)
            return;

        try
        {
            if (window == nint.Zero)
                return;

            if (iconStream.CanSeek)
                iconStream.Position = 0;

            if (!TryReadIconPixels(iconStream, out var meta, out var pixels, out var pitch))
                return;

            ReleaseIconSurface();

            var masks = SDL2Converter.ConvertSurfaceMasks((PixelFormat)meta.Format);

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                iconSurface = S2.SDL_CreateRGBSurfaceFrom(
                    handle.AddrOfPinnedObject(),
                    (int)meta.Width,
                    (int)meta.Height,
                    32, // 每像素位数
                    pitch,
                    masks.r,
                    masks.g,
                    masks.b,
                    masks.a
                );
            }
            finally
            {
                handle.Free();
            }

            if (iconSurface == nint.Zero)
            {
                Log.Error($"SDL_CreateRGBSurfaceFrom failed: {S2.SDL_GetError()}");
                return;
            }

            S2.SDL_SetWindowIcon(window, iconSurface);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            ReleaseIconSurface();
        }
        finally
        {
            if (closeStream)
            {
                try
                {
                    iconStream.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }

    private void ReleaseIconSurface()
    {
        if (iconSurface == nint.Zero)
            return;

        try
        {
            S2.SDL_FreeSurface(iconSurface);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        iconSurface = nint.Zero;
    }

    /// <summary>解析 LTX v1 流（与 SDL3Window 共用同一格式，代码各自一份）。</summary>
    private static bool TryReadIconPixels(
        Stream stream,
        out TextureResourceStreamInfo meta,
        out byte[] pixels,
        out int pitch
    )
    {
        meta = default;
        pixels = null;
        pitch = 0;

        if (!stream.CanRead)
        {
            Log.Error("[SDL2Window] icon stream is not readable.");
            return false;
        }

        Span<byte> lenBytes = stackalloc byte[2];
        if (stream.Read(lenBytes) != 2)
        {
            Log.Error("[SDL2Window] icon stream too short (missing metadata length).");
            return false;
        }

        int metaLen = BinaryPrimitives.ReadUInt16LittleEndian(lenBytes);
        var metaBytes = new byte[metaLen];

        if (stream.Read(metaBytes, 0, metaLen) != metaLen)
        {
            Log.Error($"[SDL2Window] icon stream truncated (metadata {metaLen} bytes).");
            return false;
        }

        if (!MemoryMarshal.TryRead<TextureResourceStreamInfo>(metaBytes, out meta))
        {
            Log.Error($"[SDL2Window] icon metadata malformed ({metaLen} bytes).");
            return false;
        }

        if (meta.Width == 0 || meta.Height == 0)
        {
            Log.Error($"[SDL2Window] icon has zero size ({meta.Width}x{meta.Height}).");
            return false;
        }

        if (meta.ArrayLayers > 1)
        {
            Log.Error($"[SDL2Window] icon must be a single layer, got {meta.ArrayLayers}.");
            return false;
        }

        var format = (PixelFormat)meta.Format;
        int bytesPerPixel;
        try
        {
            bytesPerPixel = format.GetBytesPerPixel();
        }
        catch (NotSupportedException ex)
        {
            Log.Error($"[SDL2Window] icon format {format} has no known byte size: {ex.Message}");
            return false;
        }

        long expected = (long)meta.Width * meta.Height * bytesPerPixel;
        var buffer = new byte[expected];

        int read = 0;
        while (read < expected)
        {
            int n = stream.Read(buffer, read, (int)(expected - read));
            if (n <= 0)
                break;
            read += n;
        }

        if (read != expected)
        {
            Log.Error($"[SDL2Window] icon pixel data short: {read}/{expected} bytes.");
            return false;
        }

        pixels = buffer;
        pitch = (int)meta.Width * bytesPerPixel;
        return true;
    }

    // ------------------------------------------------------------------ 属性落地

    private void OnAttributeChanging(WindowAttributeChangedEventArgs e)
    {
        switch (e.Attribute)
        {
            case WindowAttribute.ShowCursor:
                // 相对模式生效时强制隐藏，与 SDL3 侧保持一致。
                if (Attributes.RelativeMouse)
                    S2.SDL_ShowCursor(
                        0 /* SDL_DISABLE */
                    );
                else if ((bool)e.NewValue)
                    S2.SDL_ShowCursor(
                        1 /* SDL_ENABLE */
                    );
                else
                    S2.SDL_ShowCursor(
                        0 /* SDL_DISABLE */
                    );
                break;

            case WindowAttribute.RelativeMouse:
                if (!IsFocus)
                    break; // 非焦点窗口不碰进程级相对模式

                S2.SDL_SetRelativeMouseMode(
                    (bool)e.NewValue ? S2.SDL_bool.SDL_TRUE : S2.SDL_bool.SDL_FALSE
                );
                ApplyCursorState((bool)e.NewValue);
                break;

            case WindowAttribute.MouseSpeedScale:
                // SDL2 没有灵敏度 hint；用环境变量近似（SDL2 支持
                // SDL_HINT_MOUSE_RELATIVE_SCALE，但 ppy 绑定是否导出未验证）。
                Log.Debug("[SDL2Window] MouseSpeedScale is not supported by SDL2; change ignored.");
                e.Cancel = true;
                break;

            case WindowAttribute.TextInput:
                if ((bool)e.NewValue)
                {
                    S2.SDL_StartTextInput();
                    S2.SDL_RaiseWindow(window);
                }
                else
                    S2.SDL_StopTextInput();
                break;

            case WindowAttribute.AlwaysOnTop:
            case WindowAttribute.Opacity:
                Log.Debug(
                    $"[SDL2Window] attribute {e.Attribute} is not implemented yet; change rejected."
                );
                e.Cancel = true;
                break;
        }
    }

    // ------------------------------------------------------------------ 释放

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        Attributes.Changing -= OnAttributeChanging;

        Sdl2EventPump.Unregister(Id);

        ReleaseIconSurface();

        // 光标是进程全局状态，退出时要还原。
        try
        {
            S2.SDL_ShowCursor(
                1 /* SDL_ENABLE */
            );
            S2.SDL_SetRelativeMouseMode(S2.SDL_bool.SDL_FALSE);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        if (ownsWindow && window != nint.Zero)
            S2.SDL_DestroyWindow(window);

        window = nint.Zero;
    }
}
