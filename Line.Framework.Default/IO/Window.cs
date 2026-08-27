using Line.Framework.Graphics;
using Line.Framework.IO;
using SDL3;
using Veldrid;
using Veldrid.OpenGL;

namespace Line.Framework.Default.Graphics;

public class Window : WindowType
{
    public override RendererType Renderer { get; }
    public override ICompositor Compositor { get; }

    public Window(
        int Width = 640,
        int Height = 480,
        GraphicBackend? Backend = null,
        string Title = "Title"
    ) : base()
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

        SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events);
        Log.Debug($"Video driver: {SDL.GetCurrentVideoDriver()}");

        SDL.SetHint(SDL.Hints.TouchMouseEvents, "0");
        SDL.SetHint(SDL.Hints.MouseTouchEvents, "0");
        SDL.SetHint(SDL.Hints.WindowsEnableMessageLoop, "1");
        SDL.SetHint(SDL.Hints.WindowActivateWhenRaised, "1");
        SDL.SetHint(SDL.Hints.WindowsRawKeyboard, "1");
        SDL.SetHint(SDL.Hints.WindowsRawKeyboardInputsink, "1");

        SDL.GLSetSwapInterval(0);

        SDL.WindowFlags flags = SDL.WindowFlags.Resizable | SDL.WindowFlags.InputFocus | SDL.WindowFlags.MouseFocus;

        if (Backend == GraphicBackend.OpenGL)
            flags |= SDL.WindowFlags.OpenGL;
        if (Backend == GraphicBackend.Vulkan)
            flags |= SDL.WindowFlags.Vulkan;
        if (Backend == GraphicBackend.Metal)
            flags |= SDL.WindowFlags.Metal;

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

        //输入器
        Input = new(this);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
        MainThread.Name = "Renderer";

        if (Renderer == null)
            Renderer = new Renderer(this);
        if (Compositor == null)
            Compositor = new Compositor();

        BindEvents();
        CreateResource();

        OnWindowResized();
    }

    protected override Thread MainThread { get; }
}
