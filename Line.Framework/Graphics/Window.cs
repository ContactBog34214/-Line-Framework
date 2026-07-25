using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Line.Framework.IO;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI;
using SDL3;
using Veldrid;
using Veldrid.OpenGL;
using Veldrid.StartupUtilities;

namespace Line.Framework.Graphics;

public enum GraphicBackend
{
    Metal,
    Direct3D,
    Vulkan,
    OpenGL,
}

public class Window : IDisposable, IName
{
    public string Name => Title;
    public WindowsRenderer RendererClass { get; private set; }
    public nint WindowHandle { get; init; }
    public InputManager Input { get; init; }
    public GraphicsDevice Dev { get; init; }
    public Action RequestQuit { get; set; }
    public bool EnableMouseRelative
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
    public Vector2 Size
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
    public UIScreen Root { get; init; }
    public bool VSync
    {
        get;
        set
        {
            Dev?.SyncToVerticalBlank = value;
            field = value;
        }
    } = false;
    public bool ParallelRender { get; set; } = true;
    private readonly Thread MainThread;
    public float FramePerSecond { get; set; } = 240;
    public bool FullScreen
    {
        get;
        set
        {
            SDL.SetWindowFullscreen(WindowHandle, value);
            field = value;
        }
    } = false;

    public float UpdatePerSecond { get; set; } = 1000;
    public float Scale
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
    public CommandList commandList { get; init; }
    public UIDrawCollector Collector { get; init; }
    public GraphicBackend RenderBackend { get; init; }

    public event EventHandler<double> OnRender;

    public event EventHandler<double> OnUpdate;

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

    public Window(
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
            X = 0;
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
        WindowCreateInfo CreateInfo = new WindowCreateInfo(X, Y, Width, Height, State, Title);
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

        commandList = Dev.ResourceFactory.CreateCommandList();
        Collector = new();
        RendererClass = new(Dev);
        RendererContext = () =>
        {
            if (this != null && Collector != null)
                RendererClass.UIRenderer(this, Collector);
        };
        Root = new(this, 0, 0);

        //资源管理器
        Resource.AddType("Image", new TResourceSet(Resource, Dev, RendererClass.TextureLayout));
        Resource.AddType("Font", new TFont(Resource, Dev, RendererClass.TextureLayout));
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

    public TAudio Audio { get; private set; }
    public uint WindowID { get; init; }
    public bool IsFocus => SDL.GetKeyboardFocus() == WindowHandle;
    public bool ShowCursor
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
    internal ConcurrentDictionary<SDL.EventType, Action<SDL.Event>> EventPool { get; } = new();
    public event Action FocusGained;
    public event Action FocusLost;
    public bool Exists
    {
        get => SDL.GetWindowID(WindowHandle) != 0;
    }
    public string Title
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
                    if (UpdatePerSecond <= 0)
                    {
                        UpdatePerSecond = 1;
                    }
                    try
                    {
                        tick = sw.ElapsedTicks;
                        milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                        double delay = milliseconds - UpdateMs;
                        double wait = 1000d / UpdatePerSecond;
                        if (delay < wait)
                        {
                            if (wait - delay > 4)
                            {
                                Thread.Sleep((int)(wait - delay) - 4);
                            }
                            else
                            {
                                Thread.SpinWait((int)(wait - delay) / 4);
                            }
                            tick = sw.ElapsedTicks;
                            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                            delay = milliseconds - UpdateMs;
                        }
                        if (delay >= wait)
                        {
                            SDL.PumpEvents();
                            OnUpdate?.Invoke(this, delay);
                            UpdateMs = milliseconds;
                            while (true)
                            {
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
            void render()
            {
                if (FramePerSecond <= 0)
                {
                    FramePerSecond = 1;
                }
                try
                {
                    tick = sw.ElapsedTicks;
                    milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                    double delay = milliseconds - RenderMs;
                    double wait = 1000d / FramePerSecond;
                    if (delay < wait)
                    {
                        if (wait - delay > 4)
                        {
                            Thread.Sleep((int)(wait - delay) - 4);
                        }
                        else
                        {
                            Thread.SpinWait((int)(wait - delay) / 4);
                        }
                        tick = sw.ElapsedTicks;
                        milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                        delay = milliseconds - RenderMs;
                    }
                    if (delay >= wait)
                    {
                        OnRender?.Invoke(this, delay);
                        RenderMs = milliseconds;
                        RendererContext();
                        Dev.SwapBuffers();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex}");
                }
            }
            render();
        }
        Dispose();
    }

    Thread UpdateThread;
    private bool _resizePending = false;
    public ResourceManager Resource { get; init; }
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

    public Action RendererContext { get; init; }

    public void Dispose()
    {
        MainThread?.Interrupt();
        RendererClass = null;
        SDL.DestroyWindow(WindowHandle);
        UpdateThread?.Interrupt();
        Resource?.Dispose();
        commandList?.Dispose();
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
