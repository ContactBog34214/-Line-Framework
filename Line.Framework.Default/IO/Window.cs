using Line.Framework.Default.Graphics;
using Line.Framework.Default.Graphics.SDL3;
using Line.Framework.Default.Graphics.Veldrid;
using Line.Framework.Graphics;
using Line.Framework.IO;
using SDL3;

namespace Line.Framework.Default.IO;

public class Window : WindowType
{
    /// <summary>
    /// 用哪个窗口实现。
    /// <para>
    /// <b>SDL2 窗口只能配 Veldrid 渲染</b>：它无法提供 <c>SDL_Window*</c>，
    /// SDL3 GPU 后端没法认领它。本类目前恒定使用 Veldrid，所以两种窗口都能跑。
    /// </para>
    /// <para>
    /// ⚠️ <b>SDL2 与 SDL3 同进程共存有风险</b>：两个库各自初始化视频子系统、
    /// 各自抢键盘/鼠标/焦点。单窗口运行时通常没事，但这不是推荐配置 ——
    /// 这个开关用于验证 <see cref="IWindow"/> 抽象，不是长期方案。
    /// </para>
    /// </summary>
    public static bool UseSdl2Window = false;

    public override RendererType Renderer { get; }
    public override ICompositor Compositor { get; set; }

    /// <summary>
    /// 底层窗口。
    /// <para>
    /// <b>类型是 <see cref="IWindow"/> 而不是具体实现</b>：本类只借用它的
    /// <see cref="IWindow.Source"/> 与事件通路，不关心它是 SDL3 还是 SDL2 创建的。
    /// 原生窗口的所有权在实现那边（<c>ownsWindow: true</c>）。
    /// </para>
    /// </summary>
    protected IWindow Sdl { get; private set; }

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

        var createInfo = new WindowCreateInfo
        {
            Title = Title,
            Width = Width,
            Height = Height,
            Flags = WindowFlags.Resizable | WindowFlags.MouseFocus | WindowFlags.InputFocus,
        };

        if (UseSdl2Window)
        {
            // SDL2 自己初始化视频子系统（SDL_InitSubSystem），
            // 不需要上面那串 SDL3 的 hint。
            //
            // ⚠️ 但这意味着 SDL3 的视频子系统此时可能还没初始化 ——
            // 而本类上面的 SDL.Init(...) 已经跑过了。两套库同时在跑，
            // 见 UseSdl2Window 的说明。
            Sdl = new SDL2Window(createInfo);

            // 打印实际选中的驱动：SDL2 在 Wayland 会话下会优先 wayland，
            // 而它的 wayland 后端输入延迟明显高于 x11 —— 排查手感问题先看这行。
            Log.Debug(
                $"Window backend: SDL2, video driver: "
                    + $"{SDL2.SDL.SDL_GetCurrentVideoDriver() ?? "<none>"}"
            );
        }
        else
        {
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
            Sdl = new SDL3Window(createInfo);
        }

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

        // 始终用 Veldrid：
        //   - SDL2 窗口无法提供 SDL_Window*，SDL3 GPU 后端认领不了它；
        //   - 两条窗口路径共用同一套渲染代码，正好验证 IWindow.Source 抽象。
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
