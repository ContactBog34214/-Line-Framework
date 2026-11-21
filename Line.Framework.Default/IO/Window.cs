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

    /// <summary>
    /// 底层 SDL 窗口。
    /// <para>
    /// 本类<b>不再自己调</b> <c>SDL_CreateWindow</c> / <c>SDL_DestroyWindow</c>：
    /// 原生窗口由 <see cref="SDL3Window"/> 持有（<c>ownsWindow: true</c>），
    /// 这里只是借用它的句柄与 <see cref="IWindow.Source"/>。
    /// </para>
    /// <para>
    /// 这也是把「窗口创建」与「图形后端选择」解耦的开端：换后端时
    /// 窗口本身不需要重建，因为它已经是一次性把所有原生句柄填满了。
    /// </para>
    /// </summary>
    protected SDL3Window Sdl { get; private set; }

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

        // 视频驱动 hint 必须在 SDL_Init 之前设置，否则无效。
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

        // 窗口交给 SDL3Window 创建：标志用抽象层的 WindowFlags，
        // 不再往 SDL 传 OpenGL / Vulkan / Metal 那些会让 SDL GPU 拒绝建链的位。
        Sdl = new SDL3Window(
            new WindowCreateInfo
            {
                Title = Title,
                Width = Width,
                Height = Height,
                Flags = WindowFlags.Resizable | WindowFlags.MouseFocus | WindowFlags.InputFocus,
            }
        );

        // 交给基类的是 IWindow 抽象，而不是句柄 ——
        // 从此 WindowType 里不再有任何原生类型。
        Window = Sdl;

        var swapchainDesc = new SwapchainCreateInfo()
        {
            // 两个后端都走窗口入口：Veldrid 从 IWindow.Source 取原生句柄，
            // SDL3 从 ISdlWindowSource 取 SDL_Window*。
            // 尺寸由 SwapchainCreateInfo.Resolve 用 PixelSize 覆盖 ——
            // 这同时修掉了「HiDPI 下用逻辑尺寸建交换链会静默失败」的老问题。
            Target = SwapchainTarget.FromWindow(Sdl),
            // 交换链创建时就把当前 vsync 偏好带上（构造期设置的值存在
            // WindowType._vsync 里）。之后改 VSync 会直接转发给交换链。
            SyncToVerticalBlank = VSync,
        };

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
                Dev = new SDL3Device(
                    Backend ?? GraphicBackend.Vulkan,
                    swapchainDesc,
                    out MainSwapchain
                );
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
