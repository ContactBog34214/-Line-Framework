using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Default.Graphics.SDL3;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Types;
using SDL3;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL3 的 <see cref="IWindow"/> 实现。
/// <para>
/// <b>本类不轮询事件、不跑循环</b>。事件由 App（或你自己）泵出后按
/// <see cref="IWindow.Id"/> 分派，调用 <see cref="ProcessEvent"/> 落到这里。
/// 这样窗口不会变成「全局事件路由器」，多个窗口也才能干净共存 ——
/// 旧实现用静态 <c>PollEvents</c> 队列 + 把不属于自己的事件重新入队，
/// 多窗口时是 O(N²) 且会丢事件。
/// </para>
/// </summary>
public sealed class SDL3Window : ISdl3Window
{
    private bool ownsWindow;
    private nint window;
    private SwapchainSource source;
    private bool disposed;

    /// <summary>
    /// 已设置的图标 surface。
    /// <para>
    /// SDL 的窗口图标是「引用一个 surface」，我们必须自己持有并在下次设置
    /// 或销毁窗口时释放 —— 否则每换一次图标泄漏一张 surface。
    /// </para>
    /// </summary>
    private nint iconSurface;

    /// <summary>
    /// 队列里的条目：原生事件 + 「是否全局事件」标记 + 已复制出的文本。
    /// <para>
    /// 用一个包装结构而不是额外的哈希集合：标记必须跟着事件一起进缓冲、
    /// 一起被覆盖、一起被取走。分开存会在「事件长期不被认领」时泄漏。
    /// </para>
    /// <para>
    /// <b>为什么在这里存文本</b>：<c>SDL.Event</c> 里的字符串指针指向 SDL
    /// 内部队列的内存，下一次 <c>SDL_PumpEvents</c> 就会覆写。所以必须在事件
    /// 还活着的那一刻（入队时）复制成托管 string。用 <c>GCHandle</c> 固定没用 ——
    /// 那块内存是 SDL 分配的，不是 GC 管的。
    /// </para>
    /// </summary>
    private readonly struct QueuedEvent
    {
        public QueuedEvent(SDL.Event evt, string text, bool isGlobal)
        {
            Event = evt;
            Text = text;
            IsGlobal = isGlobal;
        }

        public SDL.Event Event { get; }

        /// <summary>已复制的文本；非文本类事件为 null。</summary>
        public string Text { get; }

        public bool IsGlobal { get; }
    }

    /// <summary>
    /// 新到的事件。由事件泵在主线程写入，由使用方（Update 循环）在另一线程读取。
    /// </summary>
    private readonly ConcurrentQueue<QueuedEvent> eventsQueue = new();

    /// <summary>
    /// 已被 filter 拒绝过的事件。
    /// <para>
    /// 「按需筛选」要求不匹配的事件不能丢 —— 下一次用别的 filter 还要能取到。
    /// 这里按到达顺序保存，满了覆盖最旧的（长时间无人认领的事件本来也没有价值）。
    /// </para>
    /// </summary>
    private readonly CircularBuffer<QueuedEvent> eventsBuffer;

    /// <summary>
    /// 事件容量，队列与暂存缓冲共用同一个值。
    /// <para>
    /// <b>必须在构造前定好</b>：<see cref="CircularBuffer{T}.Size"/> 的 setter 会
    /// 清空缓冲，运行期改等于丢掉全部未处理事件。
    /// </para>
    /// </summary>
    public int EventCapacity { get; }

    /// <summary>默认容量。</summary>
    public const int DefaultEventCapacity = 48;

    public string Name => Title;

    /// <summary><c>SDL_Window*</c>。这是逃生口，不在 <see cref="IWindow"/> 契约里。</summary>
    public nint NativeHandle => window;

    /// <summary>同 <see cref="NativeHandle"/>，见 <see cref="ISdlWindowSource"/>。</summary>
    public nint SdlWindow => window;

    /// <summary>
    /// 平台窗口 ID。
    /// <para>
    /// <c>private set</c> 而不是 get-only：两个公开构造器都转发到
    /// <c>Initialize</c>，而 get-only 自动属性只能在构造器体内赋值。
    /// </para>
    /// </summary>
    public uint Id { get; private set; }

    public WindowAttributes Attributes { get; } = new();

    public SwapchainSource Source => source;

    public event Action Resized;
    public event Action Moved;
    public event Action FocusGained;
    public event Action FocusLost;
    public event Action CloseRequested;

    /// <summary>
    /// 新建一个 SDL 窗口。
    /// <para>
    /// <b>不要传</b> <c>WindowFlags.Transparent</c>：SDL GPU 会拒绝透明窗口。
    /// 抽象层的 <see cref="WindowFlags"/> 也没有 OpenGL / Vulkan / Metal ——
    /// 那三个对 Veldrid 只是干扰项，对 SDL GPU 则是建链失败的根源。
    /// </para>
    /// </summary>
    public SDL3Window(WindowCreateInfo info, int eventCapacity = DefaultEventCapacity)
    {
        info = info.WithSanitizedSize();
        EventCapacity = eventCapacity > 0 ? eventCapacity : DefaultEventCapacity;
        eventsBuffer = new(EventCapacity);
        Initialize(info.Title, info.Width, info.Height, info.Flags);
    }

    /// <summary>新建一个 SDL 窗口（逐参数形式）。</summary>
    public SDL3Window(
        string title = "Title",
        int width = 640,
        int height = 480,
        WindowFlags flags =
            WindowFlags.Resizable | WindowFlags.InputFocus | WindowFlags.MouseFocus,
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

        // 竖屏 / 横屏提示，与旧 Window 保持一致（移动端有意义）。
        // 注意 SDL3Window 不再自己 SDL_Init 之后的 hint 顺序依赖 ——
        // 这里只设创建窗口时会被读到的 hint。
        if (width < height)
            SDL.SetHint(SDL.Hints.Orientations, "Portrait");
        else if (width > height)
            SDL.SetHint(SDL.Hints.Orientations, "Landscape");

        // SDL_Init 是引用计数的：这里只确保视频子系统可用，
        // 不负责 Quit（销毁窗口不等于关子系统）。
        SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events);

        var handle = SDL.CreateWindow(
            title,
            width,
            height,
            SDL3Converter.ConvertWindowFlags(flags)
        );
        if (handle == nint.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");

        SDL.ShowWindow(handle);

        window = handle;
        ownsWindow = true;
        Id = SDL.GetWindowID(handle);
        source = BuildSource(handle);

        Attributes.Changing += OnAttributeChanging;
        Sdl3EventPump.Register(this);
    }

    /// <summary>
    /// 包装一个已存在的 SDL 窗口。
    /// </summary>
    /// <param name="existingWindow"><c>SDL_Window*</c>。</param>
    /// <param name="ownsWindow">
    /// 是否由本对象负责 <c>SDL_DestroyWindow</c>。
    /// <para>
    /// 借用时传 <c>false</c> —— 与 <c>VeldridTexture(owns: false)</c> 是同一条防线：
    /// 包装「不拥有」的资源时，<see cref="Dispose"/> 绝不能碰它。
    /// </para>
    /// </param>
    public SDL3Window(nint existingWindow, bool ownsWindow = false)
    {
        if (existingWindow == nint.Zero)
            throw new ArgumentException("SDL_Window* is null.", nameof(existingWindow));

        EventCapacity = DefaultEventCapacity;
        window = existingWindow;
        this.ownsWindow = ownsWindow;
        Id = SDL.GetWindowID(existingWindow);
        source = BuildSource(existingWindow);
        eventsBuffer = new(EventCapacity);

        Attributes.Changing += OnAttributeChanging;
        Sdl3EventPump.Register(this);
    }

    // ------------------------------------------------------------------ Source

    /// <summary>
    /// 按当前视频驱动把所有<b>已知</b>的原生句柄一次填满。
    /// <para>
    /// 「一次填满」而非「按后端回答」是本设计的核心：窗口不知道后端是谁，
    /// 后端来挑自己那份，挑不到就抛。这样就不存在「按后端分支填 source」
    /// 那种把 <c>wl_surface*</c> 填进 SDL 槽位的机会。
    /// </para>
    /// </summary>
    private static SwapchainSource BuildSource(nint handle)
    {
        var driver = SDL.GetCurrentVideoDriver();
        var props = SDL.GetWindowProperties(handle);
        var result = new SwapchainSource();

        switch (driver)
        {
            case "wayland":
                result.Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandSurfacePointer,
                    nint.Zero
                );
                result.Connection = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandDisplayPointer,
                    nint.Zero
                );
                result.Kind = NativeWindowKind.Wayland;
                break;

            case "x11":
                result.Surface = (nint)
                    SDL.GetNumberProperty(props, SDL.Props.WindowX11WindowNumber, 0);
                result.Connection = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowX11DisplayPointer,
                    nint.Zero
                );
                result.Kind = NativeWindowKind.Xlib;
                break;

            case "windows":
                result.Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWin32HWNDPointer,
                    nint.Zero
                );
                result.Connection = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWin32InstancePointer,
                    nint.Zero
                );
                result.Kind = NativeWindowKind.Win32;
                break;

            case "cocoa":
                result.Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowCocoaWindowPointer,
                    nint.Zero
                );
                result.Kind = NativeWindowKind.Cocoa;
                break;

            case "Android":
                result.Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowAndroidSurfacePointer,
                    nint.Zero
                );
                result.Connection = SDL.GetAndroidJNIEnv();
                result.Kind = NativeWindowKind.AndroidSurface;
                break;

            default:
                Log.Warning(
                    $"SDL3Window: unknown video driver '{driver}'; SwapchainSource left empty. "
                        + "Only the SDL3 GPU backend can present to this window."
                );
                return result;
        }

        // 空句柄现在就喊出来，别留到后端去生成一句完全无关的报错。
        if (result.Surface == nint.Zero)
            Log.Warning(
                $"SDL3Window: native handle for driver '{driver}' is null; "
                    + "the Veldrid backend will not be able to present to this window."
            );

        return result;
    }

    // ------------------------------------------------------------------ 状态

    public Vector2 Size
    {
        get
        {
            SDL.GetWindowSize(window, out int w, out int h);
            return new(w, h);
        }
        set => SDL.SetWindowSize(window, (int)value.X, (int)value.Y);
    }

    public Vector2 PixelSize
    {
        get
        {
            // 必须是像素尺寸：HiDPI / 分数缩放下与逻辑尺寸不同，
            // 用错会让 surface 与 swapchain 尺寸不匹配、present 静默失败。
            SDL.GetWindowSizeInPixels(window, out int w, out int h);
            return new(w, h);
        }
    }

    public Vector2 Position
    {
        get
        {
            SDL.GetWindowPosition(window, out int x, out int y);
            return new(x, y);
        }
        set => SDL.SetWindowPosition(window, (int)value.X, (int)value.Y);
    }

    public string Title
    {
        get => SDL.GetWindowTitle(window) ?? string.Empty;
        set => SDL.SetWindowTitle(window, value);
    }

    public bool Exists => window != nint.Zero && SDL.GetWindowID(window) != 0;

    public bool IsFocus => SDL.GetKeyboardFocus() == window;

    /// <summary>鼠标是否落在本窗口上。</summary>
    public bool IsMouseFocus => SDL.GetMouseFocus() == window;

    public bool Visible
    {
        get => (SDL.GetWindowFlags(window) & SDL.WindowFlags.Hidden) == 0;
        set
        {
            if (value)
                SDL.ShowWindow(window);
            else
                SDL.HideWindow(window);
        }
    }

    public bool FullScreen
    {
        get => (SDL.GetWindowFlags(window) & SDL.WindowFlags.Fullscreen) != 0;
        set
        {
            SDL.SetWindowFullscreen(window, value);

            // SDL_SetWindowFullscreen 会重新配置窗口（改变 grab、重新关联 surface），
            // 过程中会把 relative mouse mode 与光标可见性一并重置。
            // 我们的 Attributes 记的是「用户意图」，此刻已与实际状态分叉，
            // 必须重新落地一遍 —— 否则全屏后系统光标会突然冒出来。
            ReapplyInputState();
        }
    }

    /// <summary>
    /// 把「用户意图」（相对模式 + 光标显隐）重新落地到 SDL。
    /// <para>
    /// 用于那些会**重置**窗口输入状态的平台操作之后：切换全屏、
    /// 窗口被重新配置、从最小化恢复等。SDL 在这些路径上不会保留
    /// 我们设置过的 grab / cursor 状态。
    /// </para>
    /// </summary>
    internal void ReapplyInputState()
    {
        if (window == nint.Zero)
            return;

        SDL.SetWindowRelativeMouseMode(window, Attributes.RelativeMouse);
        ApplyCursorState();
    }

    /// <summary>
    /// 只发通知，不释放任何东西 —— 由持有方决定善后
    /// （旧实现把「点 X」和「Dispose 自己」揉在一起，两者无法区分）。
    /// </summary>
    public void RequestClose() => CloseRequested?.Invoke();

    // ------------------------------------------------------------------ 事件

    /// <summary>
    /// 让本窗口的事件可读。
    /// <para>
    /// SDL3 的队列是进程级的，所以这里委托给全局泵：泵一次，
    /// 再按 WindowID 分发到各窗口。多个窗口先后调用时只有第一次真正泵，
    /// 其余因「本帧已泵过」而直接返回。
    /// </para>
    /// </summary>
    public void PumpEvents()
    {
        if (disposed)
            return;

        Sdl3EventPump.Pump(force: false);
    }

    /// <summary>
    /// 强制泵一次，跳过「本帧已泵过」的判断。
    /// <para>
    /// 用于自建主循环、或刚创建窗口需要立刻拿到事件时。
    /// 调用方需要把 <see cref="IWindow"/> 转成 <see cref="SDL3Window"/> 才能用。
    /// </para>
    /// </summary>
    public void PumpEvents(bool force)
    {
        if (disposed)
            return;

        Sdl3EventPump.Pump(force);
    }

    /// <summary>
    /// 取走第一个匹配的事件。
    /// <para>
    /// <b>顺序</b>：先看暂存缓冲（更旧），再看新到队列 —— 否则新事件会插队，
    /// 同类型事件的先后顺序会被打乱。
    /// </para>
    /// <para>
    /// <b>不匹配的不丢</b>：缓冲里不匹配的留在原位；队列里不匹配的搬进缓冲，
    /// 等下次别的 filter 来取。缓冲满了覆盖最旧的。
    /// </para>
    /// </summary>
    public bool PollEvent(out WindowEvent evt, Func<WindowEvent, bool> filter = null)
    {
        // ① 先看缓冲：更早到达的事件必须先返回。
        var count = eventsBuffer.Count;
        for (long i = 0; i < count; i++)
        {
            var queued = eventsBuffer[i];
            var candidate = new WindowEvent(Id, queued.Event, queued.IsGlobal, queued.Text);

            if (filter == null || filter(candidate))
            {
                eventsBuffer.RemoveAt(i);
                evt = candidate;
                return true;
            }
        }

        // ② 再看队列：命中即停，剩下的留在队列里等下次。
        while (eventsQueue.TryDequeue(out var queued))
        {
            var candidate = new WindowEvent(Id, queued.Event, queued.IsGlobal, queued.Text);

            if (filter == null || filter(candidate))
            {
                evt = candidate;
                return true;
            }

            eventsBuffer.Add(queued);
        }

        evt = default;
        return false;
    }

    /// <summary>
    /// 事件泵投递入口（主线程调用，原生事件此时仍然有效）。
    /// </summary>
    internal void Enqueue(SDL.Event evt) =>
        Enqueue(new QueuedEvent(evt, CaptureText(evt), isGlobal: false));

    /// <summary>
    /// 事件泵投递「无归属窗口的全局事件」。
    /// </summary>
    internal void EnqueueGlobal(SDL.Event evt) =>
        Enqueue(new QueuedEvent(evt, CaptureText(evt), isGlobal: true));

    /// <summary>
    /// 把事件里指向后端内存的字符串复制出来。
    /// <para>
    /// <b>必须在入队时调用</b>：这些指针指向 SDL 内部事件队列，
    /// 下一次 <c>SDL_PumpEvents</c> 就会覆写那块内存 —— 晚一步读到的就是垃圾
    /// （典型现象：输入框里的中文/组合字符变成乱码）。
    /// </para>
    /// <para>
    /// <c>GCHandle</c> 固定解决不了这个问题：那块内存由 SDL 分配，
    /// GC 不管它，pin 也无从谈起。唯一正确的做法就是尽早复制。
    /// </para>
    /// </summary>
    private static string CaptureText(SDL.Event evt)
    {
        switch ((SDL.EventType)evt.Type)
        {
            case SDL.EventType.TextInput:
            case SDL.EventType.TextEditing:
                return evt.Text.Text != nint.Zero
                    ? Marshal.PtrToStringUTF8(evt.Text.Text)
                    : null;

            // 拖放文本（SDL_EVENT_DROP_TEXT）暂不处理：
            // SDL3 原生字段名是 data，SDL3-CS 的映射成员名未核对过
            //（写 Text 会编译失败）。要用时查
            // ~/.nuget/packages/sdl3-cs/*/lib/*/*.xml 的 SDL.DropEvent。

            default:
                return null;
        }
    }

    private void Enqueue(QueuedEvent item)
    {
        // 有界：满了丢最旧的。事件洪水的场景下，几秒前的鼠标移动毫无价值，
        // 而「丢弃」是 SDL 自己在内部队列满时也采用的做法。
        //
        // 这条同时是防泄漏的关键：PollEvent 可能长期优先从 eventsBuffer 里取，
        // 队列若没有上限，在「缓冲总有匹配项」时会无限增长。
        while (eventsQueue.Count >= EventCapacity && eventsQueue.TryDequeue(out _)) { }

        eventsQueue.Enqueue(item);
    }

    /// <summary>把抽象层事件还原成 SDL 事件。</summary>
    public SDL.Event ToSdlEvent(WindowEvent evt) => (SDL.Event)evt.Payload;

    /// <summary>提升到前台并请求焦点。窗口管理器有权拒绝。</summary>
    public void Raise()
    {
        if (window == nint.Zero)
            return;

        SDL.RaiseWindow(window);
        SDL.ShowWindow(window);
    }

    /// <summary>
    /// 从流设置窗口图标。
    /// <para>
    /// 整段逻辑（含失败回滚与旧 surface 释放）都在实现内部，
    /// 调用方只关心「给一个流」。
    /// </para>
    /// </summary>
    public void SetIcon(Stream iconStream, bool closeStream = false)
    {
        if (iconStream is null)
            return;

        try
        {
            if (window == nint.Zero)
                return;

            // 先释放上一张，避免反复换图标时泄漏。
            ReleaseIconSurface();

            using (var io = SDL.IOFromStream(iconStream))
            {
                iconSurface = SDL.LoadSurfaceIO(io.Handle, false);

                if (iconSurface == nint.Zero)
                {
                    Log.Error($"Failed to load window icon: {SDL.GetError()}");
                    return;
                }

                if (!SDL.SetWindowIcon(window, iconSurface))
                {
                    Log.Error($"Failed to set window icon: {SDL.GetError()}");
                    ReleaseIconSurface();
                }
            }
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

    /// <summary>
    /// 立即抓取 / 释放鼠标。不改 <see cref="WindowAttributes.RelativeMouse"/> —
    /// 那是用户意图，这里只是临时状态（例如鼠标贴边时放开）。
    /// <para>
    /// <b>同时同步光标显隐</b>：旧实现的 <c>EnableMouseRelative</c> setter 里
    /// 除了设置相对模式还无条件调了 <c>SDL_HideCursor</c>，那才是光标真正被隐藏的
    /// 原因。只调 <c>SDL_SetWindowRelativeMouseMode</c> 在窗口尚未拿到焦点时
    /// 不保证隐藏光标。
    /// </para>
    /// </summary>
    public void ApplyMouseCapture(bool capture)
    {
        if (window == nint.Zero)
            return;

        SDL.SetWindowRelativeMouseMode(window, capture);
        ApplyCursorState(capture);
    }

    private void ReleaseIconSurface()    {
        if (iconSurface == nint.Zero)
            return;

        try
        {
            SDL.DestroySurface(iconSurface);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        iconSurface = nint.Zero;
    }


    // ------------------------------------------------------------------ 事件入口

    /// <summary>
    /// 由事件泵按 <see cref="Id"/> 分派进来。
    /// <para>
    /// 只处理<b>窗口级</b>事件；输入事件不在这里 —— 那是 InputManager 的事。
    /// </para>
    /// </summary>
    public void ProcessEvent(SDL.Event ev)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.WindowResized:
                Resized?.Invoke();
                break;

            case SDL.EventType.WindowMoved:
                Moved?.Invoke();
                break;

            case SDL.EventType.WindowFocusGained:
                SDL.RaiseWindow(window);
                SDL.ShowWindow(window);
                ApplyCursorState();
                SDL.SetWindowRelativeMouseMode(window, Attributes.RelativeMouse);
                FocusGained?.Invoke();
                break;

            case SDL.EventType.WindowFocusLost:
                SDL.SetWindowRelativeMouseMode(window, false);
                FocusLost?.Invoke();
                break;

            case SDL.EventType.WindowMouseEnter:
                SDL.SetWindowRelativeMouseMode(window, Attributes.RelativeMouse && IsFocus);
                ApplyCursorState();
                break;

            case SDL.EventType.WindowMouseLeave:
                SDL.SetWindowRelativeMouseMode(window, false);
                break;

            case SDL.EventType.WindowRestored:
                SDL.RaiseWindow(window);
                SDL.ShowWindow(window);
                break;

            case SDL.EventType.WindowCloseRequested:
                CloseRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// 按当前的「显示光标意图 + 相对鼠标状态」同步实际光标。
    /// <para>
    /// <b>不检查鼠标焦点</b>：<c>SDL_ShowCursor</c> / <c>SDL_HideCursor</c>
    /// 是<b>进程全局</b>状态，不是窗口级的。旧实现只在「鼠标在本窗口」时调它，
    /// 结果启动期（此刻通常还没拿到焦点）设置相对模式时光标根本没被隐藏，
    /// 表现为「鼠标被抓住但系统光标还在」。光标全局只有一个，就没有
    /// 「按窗口分别管理」这回事。
    /// </para>
    /// </summary>
    /// <param name="pendingRelativeMouse">
    /// 即将生效的相对鼠标状态。属性落地期间（Changing 内）必须显式传入 ——
    /// 那时 <see cref="WindowAttributes.RelativeMouse"/> 里还是旧值。
    /// 传 null 表示读当前值。
    /// </param>
    private void ApplyCursorState(bool? pendingRelativeMouse = null)
    {
        // 相对鼠标模式下光标必须保持隐藏 —— 否则抓取和显示会打架，
        // 表现为「鼠标被锁住但光标还在屏幕上乱飘」。
        var relative = pendingRelativeMouse ?? Attributes.RelativeMouse;

        if (Attributes.ShowCursor && !relative)
            SDL.ShowCursor();
        else
            SDL.HideCursor();
    }

    // ------------------------------------------------------------------ 属性落地

    /// <summary>
    /// 把属性表的值真正落到 SDL 上。
    /// <para>
    /// 失败就 <c>Cancel = true</c>：此时值<b>没有</b>写进表，读到的仍是旧值，
    /// 不会出现「表里说开了、实际没开」的假象。
    /// </para>
    /// </summary>
    private void OnAttributeChanging(WindowAttributeChangedEventArgs e)
    {
        switch (e.Attribute)
        {
            case WindowAttribute.ShowCursor:
                // 相对模式下光标恒隐藏 —— 否则「抓取 + 显示」会打架，
                // 表现为光标锁不住、在屏幕上乱飘。
                if (Attributes.RelativeMouse)
                    SDL.HideCursor();
                else if ((bool)e.NewValue)
                    SDL.ShowCursor();
                else
                    SDL.HideCursor();
                break;

            case WindowAttribute.RelativeMouse:
                SDL.SetWindowRelativeMouseMode(window, (bool)e.NewValue);

                // 传「即将生效」的值：Changing 在写表之前触发，
                // 直接读 Attributes.RelativeMouse 拿到的还是旧值。
                ApplyCursorState((bool)e.NewValue);
                break;

            case WindowAttribute.MouseSpeedScale:
                // SDL 只提供 hint 级别的灵敏度（全局、按相对模式生效）。
                SDL.SetHint(
                    SDL.Hints.MouseRelativeSpeedScale,
                    ((float)e.NewValue).ToString(System.Globalization.CultureInfo.InvariantCulture)
                );
                break;

            case WindowAttribute.TextInput:
                if ((bool)e.NewValue)
                {
                    SDL.StartTextInput(window);
                    SDL.RaiseWindow(window);
                }
                else
                    SDL.StopTextInput(window);
                break;

            case WindowAttribute.AlwaysOnTop:
            case WindowAttribute.Opacity:
                // 第一版明确拒绝，而不是假装成功。
                Log.Debug(
                    $"[SDL3Window] attribute {e.Attribute} is not implemented yet; change rejected."
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

        Sdl3EventPump.Unregister(Id);

        ReleaseIconSurface();

        // 光标是进程全局状态：相对模式下被我们隐藏了，
        // 退出时必须还原，否则整个桌面会没有鼠标指针。
        SDL.ShowCursor();

        if (ownsWindow && window != nint.Zero)
        {
            // SDL 要求销毁窗口在主线程。App 的循环在主线程跑，
            // 从主线程 Dispose 即为正确用法。
            SDL.DestroyWindow(window);
        }

        window = nint.Zero;
    }
}
