using Line.Framework.Default.Graphics;
using Line.Framework.Default.Graphics.SDL3;
using Line.Framework.Default.Graphics.Veldrid;
using Line.Framework.Graphics;
using Line.Framework.IO;
using SDL3;

namespace Line.Framework.Default.IO;

public class Window : WindowType
{
    public override RendererType Renderer { get; }
    public override ICompositor Compositor { get; set; }

    public Window(
        int Width = 640,
        int Height = 480,
        GraphicBackend? Backend = null,
        string Title = "Title"
    )
        : base()
    {
        //检查参数
        if (Width <= 0)
        {
            Width = 640;
        }
        if (Height <= 0)
        {
            Height = 480;
        }
        if (Backend == null || !IsBackendSupported(Backend ?? GraphicBackend.Direct3D))
        {
            Backend = BackendSelector();
        }

        Resource = new();
        //一个窗口
        if (Width < Height)
            SDL.SetHint(SDL.Hints.Orientations, "Portrait");
        else if (Width > Height)
            SDL.SetHint(SDL.Hints.Orientations, "Landscape");

        if (
            OperatingSystem.IsLinux()
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
        )
            SDL.SetHint(SDL.Hints.VideoDriver, "wayland");

        SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events);
        Log.Debug($"Video driver: {SDL.GetCurrentVideoDriver()}");

        SDL.SetHint(SDL.Hints.TouchMouseEvents, "0");
        SDL.SetHint(SDL.Hints.MouseTouchEvents, "0");
        SDL.SetHint(SDL.Hints.WindowsEnableMessageLoop, "1");
        SDL.SetHint(SDL.Hints.WindowActivateWhenRaised, "1");
        SDL.SetHint(SDL.Hints.WindowsRawKeyboard, "1");
        SDL.SetHint(SDL.Hints.WindowsRawKeyboardInputsink, "1");

        SDL.GLSetSwapInterval(0);

        SDL.WindowFlags flags =
            SDL.WindowFlags.Resizable | SDL.WindowFlags.InputFocus | SDL.WindowFlags.MouseFocus;

        /*
        if (Backend == GraphicBackend.OpenGL)
            flags |= SDL.WindowFlags.OpenGL;
        if (Backend == GraphicBackend.Vulkan)
            flags |= SDL.WindowFlags.Vulkan;
        if (Backend == GraphicBackend.Metal)
            flags |= SDL.WindowFlags.Metal;
        */

        WindowHandle = SDL.CreateWindow(Title, Width, Height, flags);
        SDL.ShowWindow(WindowHandle);
        SwapchainSource source = default;
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
                source = new()
                {
                    Surface = surface,
                    Connection = display,
                    Kind = NativeWindowKind.Wayland,
                };
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
                source = new()
                {
                    Surface = x11Window,
                    Connection = display,
                    Kind = NativeWindowKind.Xlib,
                };
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
                source = new()
                {
                    Surface = hwnd,
                    Connection = hinstance,
                    Kind = NativeWindowKind.Win32,
                };
            }
            else if (driver == "Android")
            {
                var surfaceHandle = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowAndroidSurfacePointer,
                    IntPtr.Zero
                );
                var jniEnv = SDL.GetAndroidJNIEnv();
                source = new()
                {
                    Surface = surfaceHandle,
                    Connection = jniEnv,
                    Kind = NativeWindowKind.AndroidSurface,
                };
            }
            else if (driver == "cocoa")
            {
                IntPtr nsWindow = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowCocoaWindowPointer,
                    IntPtr.Zero
                );
                source = new() { Surface = nsWindow, Kind = NativeWindowKind.Cocoa };
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

        var swapchainDesc = new SwapchainCreateInfo()
        {
            Source = source,
            Width = (uint)Width,
            Height = (uint)Height,
        };

        WindowID = SDL.GetWindowID(WindowHandle);
        bool UsingVeldrid = true;
        try
        {
            if (UsingVeldrid)
            {
                Dev = new VeldridDevice(
                    Backend ?? GraphicBackend.Vulkan,
                    swapchainDesc,
                    out MainSwapchain
                );
            }
            else
            {
                swapchainDesc.Source = new() { Surface = WindowHandle };
                Dev = new SDL3Device(Backend ?? GraphicBackend.Vulkan);
                MainSwapchain = Dev.ResourceFactory.CreateSwapchain(swapchainDesc);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
            DisposeAsync().GetAwaiter().GetResult();
            return;
        }

        RenderBackend = (GraphicBackend)Backend;

        //指令
        if (Dev == null)
        {
            Log.Error("GraphicsDevice Failed");
            DisposeAsync().GetAwaiter().GetResult();
            return;
        }

        //Log.Debug($"GraphicsDevice:{Dev} {Dev.ApiVersion}");
        Log.Debug($"GPU:{Dev.Name}");

        Collector = new();
        Root = new(this, 0, 0);

        //输入器
        Input = new(this);

        if (Renderer == null)
            Renderer = new Renderer(this, swapchainDesc);
        if (Compositor == null)
            Compositor = new Compositor();

        //渲染/更新
        RenderTask = Render(TokenSource.Token);
        UpdateTask = Update(TokenSource.Token);

        BindEvents();
        CreateResource();

        OnWindowResized();
    }

    protected override Task RenderTask { get; }
    protected override Task UpdateTask { get; }
}
