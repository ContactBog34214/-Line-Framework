using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using Line.Framework.UI;
using SDL3;

namespace Line.Framework.IO;

public abstract class WindowType : IAsyncDisposable, IName
{
#if LINUX
SDL.SetHint(SDL.HINT_VIDEO_DRIVER, "wayland,x11");
#endif

    /// <summary>
    /// 底层窗口对象。
    /// <para>
    /// <b>本类不再直接调 SDL 窗口 API</b>：句柄、尺寸、标题、焦点、全屏、
    /// 光标与文本输入全部转发给 <see cref="IWindow"/>。这样同一个
    /// <c>WindowType</c> 可以架在任意 <see cref="IWindow"/> 实现之上，
    /// 也让「窗口」这件事只剩一个真相源。
    /// </para>
    /// <para>
    /// 它是构造期由派生类赋值的 —— 本类自己不知道窗口该怎么创建，
    /// 那属于后端（<c>SDL3Window</c>）的职责。
    /// </para>
    /// </summary>
    public virtual IWindow Window { get; protected set; }

    /// <summary>
    /// 使用Stream设置窗口图标
    /// </summary>
    /// <param name="流"></param>
    /// <param name="自动关闭流"></param>
    public void SetIcon(Stream iconStream, bool closeStream = false)
    {
        Window?.SetIcon(iconStream, closeStream);
    }

    protected WindowType()
    {
        if (inited) return;
        inited = true;

        // 事件泵必须在主线程跑：SDL 在 Windows 上要求主线程，
        // 且 WindowType.Update 跑在后台任务里，不能在那里泵。
        //
        // 这里调的是 IWindow 的抽象 API，因此换后端时这行不用改。
        Entry.AddFunc(_ => Window?.PumpEvents(), -1);
    }
    private static bool inited = false;
    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Name => Title;
    /// <summary>
    /// 启用SDL事件输出
    /// </summary>
    public virtual bool EnableEventOutput { get; set; } = false;
    /// <summary>
    /// 鼠标是否落在本窗口上
    /// </summary>
    public virtual bool IsMouseFocus => Window?.IsMouseFocus ?? false;

    /// <summary>
    /// 窗口输入管理器
    /// </summary>
    public virtual InputManager Input { get; init; }

    /// <summary>
    /// Veldrid图形设备
    /// </summary>
    public virtual IGraphicsDevice Dev { get; init; }

    /// <summary>
    /// 请求退出时执行的Action
    /// </summary>
    public virtual Func<Task> RequestQuit { get; set; }

    /// <summary>
    /// 启用相对鼠标模式
    /// <para>
    /// 纯转发：值存在 <see cref="WindowAttributes"/> 里，抓取与光标显隐由
    /// <see cref="IWindow"/> 实现落地。
    /// </para>
    /// <para>
    /// <b>「推到边缘自动放人走」这类策略不在本类</b>：它在相对模式下需要一个
    /// 虚拟光标（SDL 把光标 warp 回原处，绝对位置不再变化），
    /// 属于 App 层的输入策略，等 App 落地时再实现。
    /// </para>
    /// </summary>
    public virtual bool EnableMouseRelative
    {
        get => Window?.Attributes.RelativeMouse ?? false;
        set
        {
            if (Window == null)
                return;

            Window.Attributes.RelativeMouse = value;
        }
    }

    /// <summary>
    /// 鼠标灵敏度(仅启用相对鼠标模式可用)
    /// </summary>
    public virtual float MouseSpeedScale
    {
        get => Window?.Attributes.MouseSpeedScale ?? 1f;
        set
        {
            if (Window != null && value > 0)
                Window.Attributes.MouseSpeedScale = value;
        }
    }

    /// <summary>
    /// 窗口大小（逻辑尺寸）
    /// </summary>
    public virtual Vector2 Size
    {
        get => Window?.Size ?? new(0);
        set
        {
            if (Window == null)
                return;

            try
            {
                Window.Size = value;
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
            }
        }
    }

    /// <summary>
    /// 窗口UI根节点
    /// </summary>
    public virtual UIScreen Root { get; init; }

    /// <summary>
    /// 垂直同步。
    /// <para>
    /// 转发给 <see cref="ISwapchain.SyncToVerticalBlank"/> —— 本类不自持状态。
    /// </para>
    /// <para>
    /// <b>生效是延迟的</b>（三个后端一致，都在下一次取呈现图像之前落地）：
    /// Veldrid/Vulkan 会重建交换链并先 WaitForIdle，那一帧会掉；
    /// D3D11 立即改 SyncInterval；SDL3 切 present mode。
    /// 所以设完立刻看帧率没变化是正常的。
    /// </para>
    /// <para>
    /// 交换链尚未创建时（构造期）先记在字段里，等 <see cref="MainSwapchain"/>
    /// 就绪后再接管权威值。
    /// </para>
    /// <para>
    /// <b>本属性立即生效，不做防抖。</b> 用它响应「焦点变化」这类可能连发的
    /// 事件时请改用 <see cref="RequestVSync"/>：Vulkan 每次切换都要
    /// <c>vkDeviceWaitIdle</c> 并重建整条交换链，高频反复会拖垮整机调度
    /// （表现为其它实时进程——例如 PipeWire 音频线程——出现 xrun / 爆音）。
    /// </para>
    /// </summary>
    public virtual bool VSync
    {
        get => MainSwapchain?.SyncToVerticalBlank ?? _vsync;
        set
        {
            // 显式赋值优先于任何待定的防抖请求，否则待定的那个会在之后
            // 反过来覆盖它。
            _vsyncPendingUntil = -1;
            _vsync = value;
            if (MainSwapchain != null)
                MainSwapchain.SyncToVerticalBlank = value;
        }
    }

    /// <summary>最后一次请求的 vsync 值（权威记录，不读后端）。</summary>
    private bool _vsync;

    /// <summary>
    /// 待定 vsync 请求的到期时刻（<see cref="GetStopwatchMs"/> 时间轴）。
    /// 小于 0 表示没有待定请求。
    /// </summary>
    private double _vsyncPendingUntil = -1;

    /// <summary>待定请求要应用到的值。</summary>
    private bool _vsyncPendingValue;

    /// <summary>
    /// 与 <see cref="VSync"/> 共用的时钟。单独一个实例是因为
    /// <c>Update</c> 里的 <c>sw</c> 还兼着帧间隔统计，混用容易互相干扰。
    /// </summary>
    private readonly Stopwatch _vsyncClock = Stopwatch.StartNew();

    /// <summary>
    /// 延迟合并地设置垂直同步。
    /// <para>
    /// <b>为什么需要它</b>：切换 vsync 在 Vulkan 上是重操作 ——
    /// 下一次 acquire 时先 <c>vkDeviceWaitIdle</c>，然后销毁并重建整条交换链。
    /// 而「焦点变化」这类事件会连发：alt-tab 抖动、点任务栏再点回窗口，
    /// 一秒内可能几十次，每次一次全设备排空 + 全量重建，
    /// 足以让整机的实时调度出现几百毫秒的抖动，其它进程（PipeWire 等）
    /// 会因此掉音/爆音。
    /// </para>
    /// <para>
    /// <b>语义是「延迟合并」而不是「节流」</b>：每次调用都刷新到期时刻，
    /// 只有安静 <paramref name="delayMs"/> 毫秒后没再调用，才真正应用。
    /// 所以最后一次请求一定生效，不会被吞掉。
    /// </para>
    /// <para>
    /// 由 <c>Update</c> 循环驱动，不额外起线程 —— 那个循环已经在轮询了，
    /// 借它的节拍即可（<c>UpdatePerSecond</c> 通常 1000，误差 ~1ms）。
    /// </para>
    /// </summary>
    /// <param name="value">想要的目标值。</param>
    /// <param name="delayMs">安静多久后应用。默认 250ms。</param>
    public void RequestVSync(bool value, int delayMs = 250)
    {
        _vsyncPendingValue = value;
        _vsyncPendingUntil = GetStopwatchMs(_vsyncClock) + Math.Max(0, delayMs);
    }

    /// <summary>
    /// 到期则应用待定的 vsync 请求。由 <c>Update</c> 循环每帧调用。
    /// </summary>
    private void PumpPendingVSync()
    {
        if (_vsyncPendingUntil < 0)
            return;

        if (GetStopwatchMs(_vsyncClock) < _vsyncPendingUntil)
            return;

        _vsyncPendingUntil = -1;

        // 用我们自己的记录比较，不读后端的值：
        // Veldrid/Vulkan 的 getter 返回的是「待生效值」，拿它比较会判错。
        if (_vsync != _vsyncPendingValue)
            VSync = _vsyncPendingValue;
    }

    /// <summary>
    /// 渲染频率
    /// </summary>
    public virtual float FramePerSecond { get; set; } = 240;

    /// <summary>
    /// 启用全屏
    /// </summary>
    public virtual bool FullScreen
    {
        get => Window?.FullScreen ?? false;
        set
        {
            if (Window != null)
                Window.FullScreen = value;
        }
    }

    /// <summary>
    /// 更新频率
    /// </summary>
    public virtual float UpdatePerSecond { get; set; } = 1000;

    /// <summary>
    /// 窗口内容缩放
    /// </summary>
    public virtual float Scale
    {
        get;
        set
        {
            if (value == field)
                return;
            if (value > 0)
                field = value;
            OnWindowResized();
        }
    } = 1;

    /// <summary>
    /// 窗口UI绘制收集器
    /// </summary>
    public virtual UIDrawCollector Collector { get; init; }

    /// <summary>
    /// 启用文本输入
    /// </summary>
    public virtual bool TextInput
    {
        get => Window?.Attributes.TextInput ?? false;
        set
        {
            if (Window == null)
                return;

            Window.Attributes.TextInput = value;

            // 开始输入时把窗口提到前台 —— 输入焦点需要可见窗口才有效。
            if (value)
                Window.Raise();
        }
    }

    /// <summary>
    /// 窗口渲染后端
    /// </summary>
    public virtual GraphicBackend RenderBackend { get; init; }

    /// <summary>
    /// 当渲染一帧时
    /// </summary>
    public event Action<double> OnRender;

    /// <summary>
    /// 渲染器
    /// </summary>
    public abstract RendererType Renderer { get; }

    /// <summary>
    /// 合成器
    /// </summary>
    public abstract ICompositor Compositor { get; set; }

    /// <summary>
    /// 当窗口更新时
    /// </summary>
    public event Action<double> OnUpdate;

    /// <summary>
    /// 按操作系统挑选默认后端。
    /// <para>
    /// macOS → Metal，Windows → Direct3D，其余（Linux / Android）→ Vulkan。
    /// </para>
    /// <para>
    /// 这里只做「平台级」判断，不查询驱动/设备是否真的可用 ——
    /// 静态上下文里拿不到设备，真正的能力探测必须在创建设备时进行
    /// （SDL3 用 <c>SDL_GPUSupportsShaderFormats</c>，Veldrid 用
    /// <c>GraphicsDevice.IsBackendSupported</c>）。
    /// </para>
    /// <para>
    /// <b>软件后端不会被自动选中</b>：它还没有任何后端实现，
    /// 只能由调用方显式指定。
    /// </para>
    /// </summary>
    /// <returns>该平台上应当优先使用的后端。</returns>
    public static GraphicBackend BackendSelector()
    {
        if (OperatingSystem.IsMacOS())
            return GraphicBackend.Metal;

        if (OperatingSystem.IsWindows())
            return GraphicBackend.Direct3D;

        return GraphicBackend.Vulkan;
    }

    /// <summary>
    /// 判断某个后端是否被当前平台支持。
    /// <para>
    /// <b>只反映平台约束，不代表运行时一定可用</b>：
    /// 例如 Linux 上返回 true 的 Vulkan，在没装驱动的机器上仍然可能创建失败。
    /// 需要精确能力时，请查 <see cref="IGraphicsDevice"/> 那层的设备级探测。
    /// </para>
    /// </summary>
    public static bool IsBackendSupported(GraphicBackend backend)
    {
        return backend switch
        {
            // Metal 仅 macOS 有
            GraphicBackend.Metal => OperatingSystem.IsMacOS(),

            // Direct3D 仅 Windows 有
            GraphicBackend.Direct3D => OperatingSystem.IsWindows(),

            // Vulkan：Windows / Linux / Android
            GraphicBackend.Vulkan =>
                OperatingSystem.IsWindows()
                || OperatingSystem.IsLinux()
                || OperatingSystem.IsAndroid(),

            // OpenGL 覆盖面最广，但本框架的 GL 路径尚未完善
            GraphicBackend.OpenGL =>
                OperatingSystem.IsWindows()
                || OperatingSystem.IsLinux()
                || OperatingSystem.IsAndroid(),

            // 软件渲染后端还没有实现，任何平台都不算可用
            GraphicBackend.Software => false,

            _ => false,
        };
    }

    /// <summary>
    /// 资产构建器:音频构建器
    /// </summary>
    public virtual TAudio Audio { get; protected set; }

    /// <summary>
    /// 平台窗口ID
    /// </summary>
    public virtual uint WindowID => Window?.Id ?? 0;

    /// <summary>
    /// 窗口是否为焦点
    /// </summary>
    public virtual bool IsFocus => Window?.IsFocus ?? false;

    /// <summary>
    /// 是否显示光标
    /// <para>
    /// 相对鼠标模式强制隐藏光标，所以这里是「请求值 AND 非相对模式」。
    /// 请求值本身存在 <see cref="WindowAttributes"/> 里。
    /// </para>
    /// </summary>
    public virtual bool ShowCursor
    {
        get => (Window?.Attributes.ShowCursor ?? true) && !EnableMouseRelative;
        set
        {
            if (Window == null)
                return;

            Window.Attributes.ShowCursor = value;
        }
    }
    protected internal virtual ConcurrentDictionary<SDL.EventType, Func<SDL.Event, object[], Task>> EventPool { get; } =
        new();

    /// <summary>
    /// 当窗口获得焦点时
    /// </summary>
    public virtual event Action FocusGained;

    //当窗口失去焦点时
    public virtual event Action FocusLost;

    /// <summary>
    /// 窗口是否存在
    /// </summary>
    public virtual bool Exists => Window?.Exists ?? false;

    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Title
    {
        get => Window?.Title ?? "";
        set
        {
            if (Window == null)
                return;

            try
            {
                Window.Title = value;
            }
            catch (Exception ex)
            {
                Log.Warning($"{ex.Message}");
            }
        }
    }
    protected ISwapchain MainSwapchain=default;

    protected virtual async Task Render(CancellationToken token)
    {
        Stopwatch sw = new();
        double last = 0;
        sw.Start();
        while (!token.IsCancellationRequested)
        {
            //处理
            Task invokeTask = null;
            try
            {
                if (_resizePending)
                {
                    Dev?.WaitForIdle();
                    _newWidth = (uint)Size.X;
                    _newHeight = (uint)Size.Y;
                    MainSwapchain?.Resize((int)_newWidth, (int)_newHeight);
                    _resizePending = false;
                }
                await RendererContext();
                if (token.IsCancellationRequested)
                    return;
                MainSwapchain.Swap();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            //休眠
            double waitTime = 0;
            if (FramePerSecond > 0) waitTime = 1000d / FramePerSecond;
            double ofs = GetStopwatchMs(sw) - last;
            try
            {
                invokeTask = Task.Run(() => OnRender?.Invoke(Math.Max(waitTime, ofs)), token);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, waitTime - ofs)), token);
                await invokeTask;
            }
            catch (TaskCanceledException)
            {/*_*/return; }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
            }
            last = GetStopwatchMs(sw);
        }
    }
    protected Task QuitTask = null;
    protected virtual async Task Update(CancellationToken token)
    {
        Stopwatch sw = new();
        double last = 0;
        sw.Start();
        while (!token.IsCancellationRequested)
        {
            if (QuitTask?.IsCompleted ?? false)
            {
                try
                {
                    await QuitTask;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
                QuitTask = null;
            }

            // 落地到期的防抖 vsync 请求。放在事件处理之前，
            // 这样同一帧内先应用的请求不会被这一帧的事件立刻推翻。
            PumpPendingVSync();

            // 处理事件。
            //
            // 事件已经由 IWindow 按窗口分好类，这里不需要再按 WindowID 过滤，
            // 也不存在「不属于自己就重新入队」那套写法 —— 旧实现那样做在多窗口
            // 时是 O(N²)，而且边遍历边改队列会丢事件。
            while (Window != null && Window.PollEvent(out var we))
            {
                if (token.IsCancellationRequested)
                    return;

                var ev = ToSdlEvent(we);
                var extra = TextInputExtra(we);

                if (ev.Type == (uint)SDL.EventType.WindowCloseRequested)
                {
                    try
                    {
                        if (RequestQuit != null && (QuitTask == null || QuitTask.IsCompleted))
                            QuitTask = Task.Run(RequestQuit.Invoke);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{ex}");
                    }
                }

                foreach (var item in EventPool)
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (ev.Type == (uint)item.Key)
                    {
                        try
                        {
                            if (item.Value != null)
                                await item.Value.Invoke(ev, extra);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    }
                }

                if (EnableEventOutput)
                    Log.Debug($"Event:{((SDL.EventType)ev.Type)}");
            }

            // 休眠
            double waitTime = 0;
            if (UpdatePerSecond > 0) waitTime = 1000d / UpdatePerSecond;
            double ofs = GetStopwatchMs(sw) - last;
            try
            {
                var invokeTask = Task.Run(() => OnUpdate?.Invoke(Math.Max(waitTime, ofs)), token);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, waitTime - ofs)), token);
                await invokeTask;
            }
            catch (TaskCanceledException)
            {/*_*/return; }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
            }
            last = GetStopwatchMs(sw);
        }
    }
    /// <summary>
    /// 把抽象层事件还原成 SDL 事件。
    /// <para>
    /// <b>过渡期的耦合点</b>：抽象层还没有自己的事件 union，所以只能直接解
    /// <see cref="WindowEvent.Payload"/>。本类本来就已深度耦合 SDL
    /// （<see cref="EventPool"/> 的键就是 <c>SDL.EventType</c>），这里不算新增债务；
    /// 等 core 有了完整事件类型后，本方法随之消失。
    /// </para>
    /// </summary>
    private static SDL.Event ToSdlEvent(WindowEvent we) =>
        we.Payload is SDL.Event e ? e : default;

    /// <summary>
    /// 文本输入事件需要把文本作为额外参数传给处理器。
    /// <para>
    /// 文本已在<b>入队时</b>从原生指针复制成 string（见
    /// <see cref="WindowEvent.Text"/>），这里直接取用 —— 不要再去读
    /// 原生事件里的指针，那块内存在下一次泵事件时已被覆写。
    /// </para>
    /// </summary>
    private static object[] TextInputExtra(WindowEvent we)
    {
        if (we.Text is null)
            return [];

        return [we.Text];
    }

    /// <summary>
    /// 统一出口：转发抓取状态给窗口实现。
    /// <para>
    /// 保留这一层是为了让「抓取」这件事只有一个入口 ——
    /// 将来要在这里挂钩（例如把抓取状态同步给 UI 层）时只需改一处。
    /// </para>
    /// </summary>
    private void ApplyMouseCapture(bool capture)
    {
        Window?.ApplyMouseCapture(capture);
    }

    public static double GetStopwatchMs(Stopwatch stopwatch)
    {
        if (stopwatch == null) return 0;
        return (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000.0;
    }
    protected abstract Task UpdateTask { get; }
    protected abstract Task RenderTask { get; }
    private bool _resizePending = false;
    protected virtual CancellationTokenSource TokenSource { get; set; } = new();

    /// <summary>
    /// 窗口资产管理器
    /// </summary>
    public virtual ResourceManager Resource { get; init; }
    private uint _newWidth,
        _newHeight;

    protected virtual void OnWindowResized()
    {
        _resizePending = true;
        _newWidth = (uint)Size.X;
        _newHeight = (uint)Size.Y;
        Root.UpdateScreenSize((int)_newWidth, (int)_newHeight);
    }

    public virtual async Task RendererContext()
    {
        if (this != null && Collector != null)
        {
            if (Compositor == null || Renderer == null)
                return;
            var cp = await Compositor?.Composite(Root);
            if (cp != null)
                Renderer?.Render(cp, MainSwapchain?.RenderTarget);
            foreach (var i in cp)
            {
                Recyclable<Vertex>.Free(i);
            }
        }
    }
    protected int _isDisposed = 0;
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;
    public virtual async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;
        await TokenSource.CancelAsync();
        if (RenderTask != null) await RenderTask;
        try
        {
            Resource?.Dispose();
            Root?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }

        Renderer?.Dispose();
        try
        {
            Dev?.WaitForIdle();
            Dev?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning($"{ex.Message}");
        }
        Entry.DoOnMainThread(_ =>
        {
            // 窗口的所有权在 IWindow 实现那边，本类不再持有任何原生资源。
            Window?.Dispose();

            // 置空：泵事件的 lambda 是 `Window?.PumpEvents()`，不置空它会一直
            // 往一个已销毁的窗口泵，产生「事件无处可去」的日志噪音。
            Window = null;
        });
    }

    /// <summary>
    /// 获取全屏模式
    /// </summary>
    /// <param name="显示器ID"></param>
    /// <returns>全屏模式数组</returns>
    public static FullScreenMode[] GetFullScreenModes(uint display)
    {
        var s = SDL.GetFullscreenDisplayModes(display, out int _);
        List<FullScreenMode> tmp = [];
        foreach (var item in s)
        {
            tmp.Add(new(new(item.W, item.H), item.RefreshRate, item.PixelDensity));
        }
        return tmp.ToArray();
    }

    protected virtual void BindEvents()
    {
        //绑定事件
        EventPool.TryAdd(
            SDL.EventType.WindowResized,
            async (a, _) =>
            {
                OnWindowResized();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusGained,
            async (a, _) =>
            {
                Window?.Raise();

                // 无条件重试抓取：窗口刚拿到焦点时，之前那次设置相对模式
                // 往往是被窗口管理器忽略掉的（没有焦点就没有抓取的语义）。
                // 不要再判断 IsMouseFocus —— 焦点事件期间它可能还是旧值，
                // 条件判断只会让「启动时鼠标已在内」这条常见路径失效。
                ApplyMouseCapture(EnableMouseRelative);
                FocusGained?.Invoke();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowMouseEnter,
            async (a, _) =>
            {
                // 只同步抓取状态。光标显隐由 ApplyMouseCapture 内部一起做 ——
                // 这里不要再写 Attributes.ShowCursor：它的 getter 带了
                // 「与相对模式做与运算」的派生逻辑，写回会改写用户意图。
                //
                // 不再要求 IsFocus：旧实现的等价代码虽然带了该条件，但它
                // 紧接着无条件调了 Show/HideCursor，所以光标照样被隐藏；
                // 现在把光标处理收进 ApplyMouseCapture 后，多那个条件只会
                // 让抓取和光标状态都不生效。
                ApplyMouseCapture(EnableMouseRelative);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowMouseLeave,
            async (a, _) =>
            {
                ApplyMouseCapture(false);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowRestored,
            async (a, _) =>
            {
                Window?.Raise();

                // 从最小化恢复同样会重置 grab / 光标状态。
                ApplyMouseCapture(EnableMouseRelative);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusLost,
            async (a, _) =>
            {
                ApplyMouseCapture(false);
                FocusLost?.Invoke();
            }
        );

        // 全屏切换会重置窗口的 grab 与光标可见性（SDL 内部重配 surface）。
        // 程序调用 Host.FullScreen 时 IWindow 已在 setter 里重落地，
        // 但窗口管理器直接全屏（F11、WM 快捷键）不会经过我们的 setter，
        // 所以这里也要接一次。
        EventPool.TryAdd(
            SDL.EventType.WindowEnterFullscreen,
            async (a, _) =>
            {
                ApplyMouseCapture(EnableMouseRelative);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowLeaveFullscreen,
            async (a, _) =>
            {
                ApplyMouseCapture(EnableMouseRelative);
            }
        );

        RequestQuit = async () => await DisposeAsync();
    }

    protected virtual void CreateResource()
    {
        Audio = new TAudio(Resource);
        Resource.AddType("Audio", Audio);
        Resource.AddType("Image", new TResourceSet(Dev, Renderer.TextureLayout));
        Resource.AddType("Font", new TFont(Dev, Renderer.TextureLayout));
    }
}
