using Line.Framework.Default.Graphics;
using Line.Framework.Graphics;
using Line.Framework.IO;
using SDL3;
using Veldrid;
using Veldrid.OpenGL;

namespace Line.Framework.Default.IO;

public class AndroidActity : WindowType
{
    public virtual nint ActivityHandle => WindowHandle;
    public override RendererType Renderer { get; }
    public override ICompositor Compositor { get; }
    public override float FramePerSecond { get; set; } = 144;
    public override float UpdatePerSecond { get; set; } = 1000;
    public AndroidActity(
        GraphicBackend? Backend = null,
        string Title = "Title"
    )
    {
        if (Backend == null || !IsBackendSupported(Backend ?? GraphicBackend.Direct3D))
        {
            Backend = BackendSelector();
        }

        Resource = new();
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

        SDL.Init(SDL.InitFlags.Video);

        WindowHandle = SDL.CreateWindow(Title, 640, 480, flags);
        SDL.ShowWindow(WindowHandle);
        SwapchainSource source = null;
        var driver = SDL.GetCurrentVideoDriver();

        try
        {
            uint props = SDL.GetWindowProperties(WindowHandle);
            if (driver == "Android")
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

        SDL.GetWindowSize(WindowHandle, out var Width, out var Height);

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
                    goto case GraphicBackend.Vulkan;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
            if (GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan))
                Dev = GraphicsDevice.CreateVulkan(Options, swapchainDesc);
            else
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

        Log.Debug($"GraphicsDevice:{Dev.BackendType} {Dev.ApiVersion}");
        Log.Debug($"GPU:{Dev.DeviceName}");

        Collector = new();
        Root = new(this, 0, 0);

        //渲染/更新
        RenderTask = Render(TokenSource.Token);
        UpdateTask = Update(TokenSource.Token);

        if (Renderer == null)
            Renderer = new Renderer(this);
        if (Compositor == null)
            Compositor = new Compositor();

        BindEvents();
        CreateResource();

        OnWindowResized();
    }

    public override event Action FocusGained;
    public override event Action FocusLost;
    protected override Task RenderTask { get; }
    protected override Task UpdateTask { get; }
}
