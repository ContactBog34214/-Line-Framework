using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Input;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI;
using SDL3;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vulkan;
using Vulkan.Wayland;
using UIScreen = Line.Framework.UI.UIScreen;

namespace Line.Framework.Graphics;

public class BaseWindow : IDisposable
{
    public WindowsRenderer RendererClass { get; private set; }
    public nint WindowHandle { get; init; }
    public InputManager Input { get; init; }
    public GraphicsDevice Dev { get; init; }
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
                Log.Warning($"[Window] {ex}");
            }
        }
    }
    public UIScreen Root { get; init; }
    private readonly Thread MainThread;
    public float FramePerSecond { get; set; } = 240;
    public float UpdatePerSecond { get; set; } = 1000;
    public CommandList commandList { get; init; }
    public UIDrawCollector Collector { get; init; }

    public event EventHandler<double> OnRender;

    public event EventHandler<double> OnUpdate;

    public static GraphicsBackend BackendSelector()
    {
        //建个队列（简单的不会搞）😭
        GraphicsBackend[] queue =
        {
            GraphicsBackend.Metal,
            GraphicsBackend.Vulkan,
            GraphicsBackend.Direct3D11,
            GraphicsBackend.OpenGL,
            GraphicsBackend.OpenGLES,
        };
        //默认设备（到最后都用不了那就算了吧）
        GraphicsBackend? Choice = null;
        foreach (GraphicsBackend backend in queue)
        {
            if (GraphicsDevice.IsBackendSupported(backend))
            {
                Choice = backend;
                break;
            }
        }
        //代码死犟死犟的，就这样吧～
        return (GraphicsBackend)Choice;
    }

    public BaseWindow(
        int X = 0,
        int Y = 0,
        int Width = 640,
        int Height = 480,
        WindowState State = WindowState.Normal,
        GraphicsBackend? Backend = null,
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
        WindowCreateInfo CreateInfo = new WindowCreateInfo(X, Y, Width, Height, State, Title);
        //一个窗口
        SDL.Init(SDL.InitFlags.Video);
        SDL.WindowFlags flags = SDL.WindowFlags.Vulkan | SDL.WindowFlags.Resizable;
        WindowHandle = SDL.CreateWindow(Title, Width, Height, flags);
        SDL.SetHint("SDL_HINT_TOUCH_MOUSE_EVENTS", "0");
        WindowID = SDL.GetWindowID(WindowHandle);
        SwapchainDescription sc=new();
        GraphicsDeviceOptions Options = new GraphicsDeviceOptions
        {
            //自动otto
            Debug = false,
            PreferStandardClipSpaceYDirection = true,
            SwapchainSrgbFormat = false,
            SyncToVerticalBlank = false,
        };
        Dev = GraphicsDevice.CreateVulkan(Options,sc);
        //指令
        if (Dev == null)
        {
            Dispose();
            return;
        }

        commandList = Dev.ResourceFactory.CreateCommandList();
        Collector = new();
        RendererClass = new(Dev);
        RendererContext = () =>
        {
            RendererClass.UIRenderer(this, Collector);
        };
        Root = new(this, 0, 0);

        //资源管理器
        Resource = new();
        Resource.AddType("Image", new TResourceSet(Resource, Dev, RendererClass.TextureLayout));
        Resource.AddType("Font", new TFont(Resource, Dev, RendererClass.TextureLayout));
        Audio = new TAudio(Resource);
        Resource.AddType("Audio", Audio);
        //输入器
        Input = new(this);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
        MainThread.Name = "Renderer";

        //绑定事件
    }

    public TAudio Audio { get; private set; }
    public uint WindowID { get; init; }
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
                Log.Warning($"[Window] {ex.Message}");
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
            unsafe void update()
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
                                Thread.Sleep((int)(wait - delay) - 2);
                            }
                            else
                            {
                                Thread.SpinWait((int)(wait - delay) / 2);
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
                                        return b.Window.WindowID == WindowHandle;
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
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Update]{ex}");
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
                Dev.MainSwapchain.Resize(_newWidth, _newHeight);
                _resizePending = false;
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
                            Thread.Sleep((int)(wait - delay) - 2);
                        }
                        else
                        {
                            Thread.SpinWait((int)(wait - delay) / 2);
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
                    Log.Error($"[Renderer]{ex}");
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
        SDL.GetWindowSize(WindowHandle, out int w, out int h);
        _newWidth = (uint)w;
        _newHeight = (uint)h;
        Root.UpdateScreenSize(w, h);
    }

    public Action RendererContext { get; init; }

    public void Dispose()
    {
        MainThread?.Interrupt();
        RendererClass = null;
        SDL.DestroyWindow(WindowHandle);
        UpdateThread?.Interrupt();
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
        Resource?.Dispose();
    }
}
