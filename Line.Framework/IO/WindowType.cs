using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using Line.Framework.UI;
using SDL3;
using Veldrid;
using Veldrid.OpenGL;

namespace Line.Framework.IO;

public abstract class WindowType : IDisposable, IName
{
    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Name => Title;

    /// <summary>
    /// 窗口SDL3句柄
    /// </summary>
    public virtual nint WindowHandle { get; init; }

    /// <summary>
    /// 窗口输入管理器
    /// </summary>
    public virtual InputManager Input { get; init; }

    /// <summary>
    /// Veldrid图形设备
    /// </summary>
    public virtual GraphicsDevice Dev { get; init; }

    /// <summary>
    /// 请求退出时执行的Action
    /// </summary>
    public virtual Action RequestQuit { get; set; }

    /// <summary>
    /// 启用相对鼠标模式
    /// </summary>
    public virtual bool EnableMouseRelative
    {
        get => SDL.GetWindowRelativeMouseMode(WindowHandle);
        set
        {
            SDL.SetWindowRelativeMouseMode(WindowHandle, value);
            if (ShowCursor)
                SDL.ShowCursor();
            else
                SDL.HideCursor();
        }
    }

    /// <summary>
    /// 鼠标灵敏度(仅启用相对鼠标模式可用)
    /// </summary>
    public virtual float MouseSpeedScale
    {
        get;
        set
        {
            if (value > 0)
                field = value;
        }
    } = 1;

    /// <summary>
    /// 窗口大小
    /// </summary>
    public virtual Vector2 Size
    {
        get
        {
            try
            {
                SDL.GetWindowSize(WindowHandle, out int w, out int h);
                return new(w, h);
            }
            catch
            {
                return new(0);
            }
        }
        set
        {
            try
            {
                SDL.SetWindowSize(WindowHandle, (int)value.X, (int)value.Y);
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
    /// 垂直同步
    /// </summary>
    public virtual bool VSync
    {
        get;
        set
        {
            Dev?.SyncToVerticalBlank = value;
            field = value;
        }
    } = false;
    private readonly Thread MainThread;

    /// <summary>
    /// 渲染频率
    /// </summary>
    public virtual float FramePerSecond { get; set; } = 240;

    /// <summary>
    /// 启用全屏
    /// </summary>
    public virtual bool FullScreen
    {
        get;
        set
        {
            SDL.SetWindowFullscreen(WindowHandle, value);
            field = value;
        }
    } = false;

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
        get => SDL.TextInputActive(WindowHandle);
        set
        {
            if (value)
                SDL.StartTextInput(WindowHandle);
            else
                SDL.StopTextInput(WindowHandle);
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
    public abstract ICompositor Compositor { get; }

    /// <summary>
    /// 当窗口更新时
    /// </summary>
    public event Action<double> OnUpdate;

    /// <summary>
    /// 后端选择器
    /// </summary>
    /// <returns>可用后端</returns>
    public static GraphicBackend BackendSelector()
    {
        //默认设备（到最后都用不了那就算了吧）
        int Choice = 0;
        for (int i = 0; i < 5; i++)
        {
            GraphicsBackend backend;
            switch (i)
            {
                case 1:
                    backend = GraphicsBackend.Metal;
                    break;
                case 2:
                    backend = GraphicsBackend.Direct3D11;
                    break;
                case 3:
                    backend = GraphicsBackend.Vulkan;
                    break;
                case 4:
                    backend = GraphicsBackend.OpenGL;
                    break;
                default:
                    backend = GraphicsBackend.Vulkan;
                    break;
            }

            if (GraphicsDevice.IsBackendSupported(backend))
            {
                Choice = i;
                break;
            }
        }
        //代码死犟死犟的，就这样吧～
        return (GraphicBackend)Choice;
    }

    protected WindowType(
        int X = 0,
        int Y = 0,
        int Width = 640,
        int Height = 480,
        WindowState State = WindowState.Normal,
        GraphicBackend? Backend = null,
        string Title = "Title"
    )
    {
        //检查参数
        if (X < 0)
        {
            X = 0;
        }
        if (Y < 0)
        {
            Y = 0;
        }
        if (Width <= 0)
        {
            Width = 640;
        }
        if (Height <= 0)
        {
            Height = 480;
        }
        if (Backend == null)
        {
            Backend = BackendSelector();
        }

        Resource = new();
        //一个窗口
        if (Width < Height)
            SDL.SetHint(SDL.Hints.Orientations, "Portrait");
        else if (Width > Height)
            SDL.SetHint(SDL.Hints.Orientations, "Landscape");
        SDL.SetHint(SDL.Hints.VideoDriver, "wayland");
        SDL.Init(SDL.InitFlags.Video);
        Log.Debug($"Video driver: {SDL.GetCurrentVideoDriver()}");
        SDL.SetHint(SDL.Hints.TouchMouseEvents, "0");
        SDL.SetHint(SDL.Hints.MouseTouchEvents, "0");
        SDL.GLSetSwapInterval(0);

        SDL.WindowFlags flags = SDL.WindowFlags.Resizable;

        if (Backend == GraphicBackend.OpenGL)
            flags = flags | SDL.WindowFlags.OpenGL;
        if (Backend == GraphicBackend.Vulkan)
            flags = flags | SDL.WindowFlags.Vulkan;
        if (Backend == GraphicBackend.Metal)
            flags = flags | SDL.WindowFlags.Metal;

        WindowHandle = SDL.CreateWindow(Title, Width, Height, flags);
        SDL.ShowWindow(WindowHandle);
        SwapchainSource source = null;
        var driver = SDL.GetCurrentVideoDriver();

        try
        {
            uint props = SDL.GetWindowProperties(WindowHandle);
            if (driver == "wayland")
            {
                IntPtr display = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandDisplayPointer,
                    IntPtr.Zero
                );
                IntPtr surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandSurfacePointer,
                    IntPtr.Zero
                );
                source = SwapchainSource.CreateWayland(display, surface);
            }
            else if (driver == "x11")
            {
                var display = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowX11DisplayPointer,
                    IntPtr.Zero
                );
                var x11Window = (IntPtr)
                    SDL.GetNumberProperty(props, SDL.Props.WindowX11WindowNumber, 0);
                source = SwapchainSource.CreateXlib(display, x11Window);
            }
            else if (driver == "windows")
            {
                var hwnd = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWin32HWNDPointer,
                    IntPtr.Zero
                );
                var hinstance = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWin32InstancePointer,
                    IntPtr.Zero
                );
                source = SwapchainSource.CreateWin32(hwnd, hinstance);
            }
            else if (driver == "Android")
            {
                var surfaceHandle = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowAndroidSurfacePointer,
                    IntPtr.Zero
                );
                var jniEnv = SDL.GetAndroidJNIEnv();
                source = SwapchainSource.CreateAndroidSurface(surfaceHandle, jniEnv);
            }
            else if (driver == "cocoa")
            {
                IntPtr nsWindow = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowCocoaWindowPointer,
                    IntPtr.Zero
                );
                source = SwapchainSource.CreateNSWindow(nsWindow);
            }
            else
            {
                Log.Error($"What is {driver}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }

        Width = (int)Size.X;
        Height = (int)Size.Y;

        var swapchainDesc = new SwapchainDescription(
            source,
            (uint)Width,
            (uint)Height,
            null, // 深度格式，可选
            false // 垂直同步
        );

        WindowID = SDL.GetWindowID(WindowHandle);
        GraphicsDeviceOptions Options = new GraphicsDeviceOptions
        {
            //自动otto
            Debug = false,
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = false,
        };
        try
        {
            switch (Backend)
            {
                case GraphicBackend.Metal:
                    if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Metal))
                        Dev = GraphicsDevice.CreateMetal(Options, swapchainDesc);
                    break;
                case GraphicBackend.Direct3D:
                    if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Direct3D11))
                        Dev = GraphicsDevice.CreateD3D11(Options, swapchainDesc);
                    break;
                case GraphicBackend.Vulkan:
                    if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan))
                        Dev = GraphicsDevice.CreateVulkan(Options, swapchainDesc);
                    break;
                case GraphicBackend.OpenGL:
                    if (GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGL))
                    {
                        nint GLContext = SDL.GLCreateContext(WindowHandle);
                        var info = new OpenGLPlatformInfo(
                            openGLContextHandle: GLContext,
                            getProcAddress: (name) => SDL.GLGetProcAddress(name),
                            makeCurrent: (ctx) => SDL.GLMakeCurrent(WindowHandle, ctx), // 必须返回 bool
                            getCurrentContext: SDL.GLGetCurrentContext,
                            clearCurrentContext: () => SDL.GLMakeCurrent(WindowHandle, IntPtr.Zero),
                            deleteContext: (ctx) => SDL.GLDestroyContext(ctx),
                            swapBuffers: () => SDL.GLSwapWindow(WindowHandle),
                            setSyncToVerticalBlank: (enabled) =>
                                SDL.GLSetSwapInterval(enabled ? 1 : 0)
                        );
                        SDL.GLSetSwapInterval(0);

                        Dev = GraphicsDevice.CreateOpenGL(
                            Options,
                            info,
                            (uint)Size.X,
                            (uint)Size.Y
                        );
                    }
                    break;
                default:
                    if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan))
                        Dev = GraphicsDevice.CreateVulkan(Options, swapchainDesc);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
            if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan))
                Dev = GraphicsDevice.CreateVulkan(Options, swapchainDesc);
            else
                Dispose();
            return;
        }

        RenderBackend = (GraphicBackend)Backend;

        //指令
        if (Dev == null)
        {
            Log.Error("GraphicsDevice Failed");
            Dispose();
            return;
        }

        Log.Debug($"GraphicsDevice:{Dev.BackendType} {Dev.ApiVersion}");
        Log.Debug($"GPU:{Dev.DeviceName}");

        Collector = new();
        Root = new(this, 0, 0);

        //资源管理器
        Audio = new TAudio(Resource);
        Resource.AddType("Audio", Audio);
        OnWindowResized();
        //输入器
        Input = new(this);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
        MainThread.Name = "Renderer";

        //绑定事件
        EventPool.TryAdd(
            SDL.EventType.WindowResized,
            (a) =>
            {
                OnWindowResized();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusGained,
            (a) =>
            {
                if (SDL.GetMouseFocus() == WindowHandle)
                {
                    if (ShowCursor)
                        SDL.ShowCursor();
                    else
                        SDL.HideCursor();
                }
                FocusGained?.Invoke();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusLost,
            (a) =>
            {
                if (SDL.GetMouseFocus() == WindowHandle)
                {
                    if (ShowCursor)
                        SDL.ShowCursor();
                    else
                        SDL.HideCursor();
                }
                FocusLost?.Invoke();
            }
        );
        RequestQuit = Dispose;
    }

    /// <summary>
    /// 资产构建器:音频构建器
    /// </summary>
    public virtual TAudio Audio { get; private set; }

    /// <summary>
    /// SDL3窗口ID
    /// </summary>
    public virtual uint WindowID { get; init; }

    /// <summary>
    /// 窗口是否为焦点
    /// </summary>
    public virtual bool IsFocus => SDL.GetKeyboardFocus() == WindowHandle;

    /// <summary>
    /// 是否显示光标
    /// </summary>
    public virtual bool ShowCursor
    {
        get => field && EnableMouseRelative;
        set
        {
            field = value;
            if (SDL.GetMouseFocus() == WindowHandle)
            {
                if (field)
                    SDL.ShowCursor();
                else
                    SDL.HideCursor();
            }
        }
    } = true;
    internal virtual ConcurrentDictionary<SDL.EventType, Action<SDL.Event>> EventPool { get; } =
        new();

    /// <summary>
    /// 当窗口获得焦点时
    /// </summary>
    public event Action FocusGained;

    //当窗口失去焦点时
    public event Action FocusLost;

    /// <summary>
    /// 窗口是否存在
    /// </summary>
    public virtual bool Exists
    {
        get => SDL.GetWindowID(WindowHandle) != 0;
    }

    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Title
    {
        get => SDL.GetWindowTitle(WindowHandle) ?? "";
        set
        {
            try
            {
                SDL.SetWindowTitle(WindowHandle, value);
            }
            catch (NullReferenceException ex)
            {
                Log.Warning($"{ex.Message}");
            }
        }
    }

    private void UpdateWindow()
    {
        var sw = new Stopwatch();
        sw.Start();
        long tick = sw.ElapsedTicks;
        double milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
        double RenderMs = 0;
        //开始考试
        while (Exists)
        {
            tick = sw.ElapsedTicks;
            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;

            //输入更新
            void update()
            {
                double UpdateMs = 0;
                while (Exists)
                {
                    //防止冻结
                    if (UpdatePerSecond <= 0 && UpdatePerSecond != -1)
                    {
                        UpdatePerSecond = -1;
                    }
                    try
                    {
                        tick = sw.ElapsedTicks;
                        milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                        double delay = milliseconds - UpdateMs;
                        double wait = 1000d / UpdatePerSecond;
                        if (UpdatePerSecond == -1)
                            wait = 0;
                        if (delay < wait)
                        {
                            Task.Delay(TimeSpan.FromMicroseconds(wait - delay))
                                .GetAwaiter()
                                .GetResult();
                            tick = sw.ElapsedTicks;
                            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                            delay = milliseconds - UpdateMs;
                        }
                        if (delay >= wait)
                        {
                            SDL.PumpEvents();
                            OnUpdate?.Invoke(delay);
                            UpdateMs = milliseconds;
                            while (true)
                            {
                                if (IsFocus)
                                    SDL.SetHint(
                                        SDL.Hints.MouseRelativeSpeedScale,
                                        MouseSpeedScale.ToString()
                                    );
                                SDL.SetEventFilter(
                                    (a, ref b) =>
                                    {
                                        if (b.Type == (uint)SDL.EventType.WindowCloseRequested)
                                            RequestQuit?.Invoke();
                                        return (
                                                b.Window.WindowID == WindowID
                                                || b.TFinger.WindowID == WindowID
                                            )
                                            && b.Type != (uint)SDL.EventType.WindowCloseRequested;
                                    },
                                    (nint)WindowID
                                );

                                var events = SDL.PollEvent(out var ev);

                                if (!events)
                                    break;
                                foreach (var item in EventPool)
                                {
                                    if (ev.Type == (uint)item.Key)
                                    {
                                        item.Value?.Invoke(ev);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{ex}");
                    }
                }
            }
            //开更新线程
            if (
                UpdateThread == null
                || UpdateThread.ThreadState == System.Threading.ThreadState.Stopped
            )
            {
                UpdateThread?.Interrupt();
                UpdateThread = new(update);
                UpdateThread.Start();
            }

            //处理大小更新
            if (_resizePending)
            {
                Dev.WaitForIdle();
                _newWidth = (uint)Size.X;
                _newHeight = (uint)Size.Y;
                Dev.MainSwapchain.Resize(_newWidth, _newHeight);
                _resizePending = false;
                Dev?.SyncToVerticalBlank = VSync;
            }

            //正式渲染
            async Task render()
            {
                if (FramePerSecond <= 0 && FramePerSecond != -1)
                {
                    FramePerSecond = -1;
                }
                try
                {
                    tick = sw.ElapsedTicks;
                    milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                    double delay = milliseconds - RenderMs;
                    double wait = 1000d / FramePerSecond;
                    if (FramePerSecond == -1)
                        wait = 0;
                    if (delay < wait)
                    {
                        await Task.Delay(TimeSpan.FromMicroseconds(wait - delay));

                        tick = sw.ElapsedTicks;
                        milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                        delay = milliseconds - RenderMs;
                    }
                    if (delay >= wait)
                    {
                        OnRender?.Invoke(delay);
                        RenderMs = milliseconds;
                        RendererContext().GetAwaiter().GetResult();
                        Dev.SwapBuffers();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex}");
                }
            }
            render().GetAwaiter().GetResult();
        }
        Dispose();
    }

    Thread UpdateThread;
    private bool _resizePending = false;

    /// <summary>
    /// 窗口资产管理器
    /// </summary>
    public virtual ResourceManager Resource { get; init; }
    private uint _newWidth,
        _newHeight;

    private void OnWindowResized()
    {
        _resizePending = true;
        SDL.GetWindowSize(WindowHandle, out _, out _);
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
                Renderer?.Render(cp);
        }
    }

    public virtual void Dispose()
    {
        SDL.DestroyWindow(WindowHandle);
        Renderer?.Dispose();
        Resource?.Dispose();
        try
        {
            Dev?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning($"{ex.Message}");
        }
        Root?.Dispose();
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
}
