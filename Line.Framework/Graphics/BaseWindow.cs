using System.Diagnostics;
using Line.Framework.Input;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using UIScreen = Line.Framework.UI.UIScreen;

namespace Line.Framework.Graphics;

public class BaseWindow : IDisposable
{
    public WindowsRenderer RendererClass { get; private set; }
    public Sdl2Window TargetWindow { get; init; }
    public InputManager Input { get; init; }
    public GraphicsDevice Dev { get; init; }
    public UIScreen Root { get; init; }
    private Thread MainThread;
    public float FramePerSecond { get; set; } = 10;
    public float UpdatePerSecond { get; set; } = 1000;
    public CommandList commandList { get; init; }
    public UIDrawCollector Collector { get; init; } = new();

    //更新事件💩
    public class OnRenderArgs : EventArgs
    {
        public double delay;
    }

    public event EventHandler<OnRenderArgs> OnRender;

    public class OnUpdateArgs : EventArgs
    {
        public double delay;
    }

    public event EventHandler<OnUpdateArgs> OnUpdate;

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
        TargetWindow = VeldridStartup.CreateWindow(CreateInfo);
        GraphicsDeviceOptions Options = new GraphicsDeviceOptions
        {
            //自动otto
            Debug = Debugger.IsAttached,
            PreferStandardClipSpaceYDirection = true,
            SwapchainSrgbFormat = false,
            SyncToVerticalBlank = false,
        };
        try
        {
            Dev = VeldridStartup.CreateGraphicsDevice(
                TargetWindow,
                Options,
                (GraphicsBackend)Backend
            );
        }
        catch
        {
            Options.Debug = false;
            Dev = VeldridStartup.CreateGraphicsDevice(
                TargetWindow,
                Options,
                (GraphicsBackend)Backend
            );
        }
        //指令
        commandList = Dev.ResourceFactory.CreateCommandList();
        Collector = new();
        RendererClass = new(Dev);
        RendererContext = () =>
        {
            RendererClass.UIRenderer(this, Collector);
        };
        TargetWindow.Resized += OnWindowResized;
        Root = new(this, 0, 0);
        Root.UpdateScreenSize(TargetWindow.Width, TargetWindow.Height);

        //资源管理器
        Resource = new();
        Resource.AddType("Image", new TResourceSet(Resource, Dev, RendererClass.TextureLayout));
        Resource.AddType("Font", new TFont(Resource, Dev, RendererClass.TextureLayout));
        Audio=new TAudio(Resource);
        Resource.AddType("Audio", Audio);
        //输入器
        Input = new(TargetWindow);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
    }

    public TAudio Audio{get;private set;}

    private void UpdateWindow()
    {
        var sw = new Stopwatch();
        sw.Start();
        long tick = sw.ElapsedTicks;
        double milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
        double RenderMs = 0;
        //开始考试
        while (TargetWindow.Exists)
        {
            tick = sw.ElapsedTicks;
            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;

            //输入更新
            void update()
            {
                long tick = sw.ElapsedTicks;
                double milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                double UpdateMs = 0;
                while (TargetWindow.Exists)
                {
                    tick = sw.ElapsedTicks;
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
                                Thread.Sleep((int)(wait - delay));
                            }
                            else
                            {
                                Thread.SpinWait((int)(wait - delay));
                            }
                            tick = sw.ElapsedTicks;
                            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                            delay = milliseconds - UpdateMs;
                        }
                        if (delay >= wait)
                        {
                            var args = new OnUpdateArgs { delay = delay };
                            TargetWindow.PumpEvents();
                            OnUpdate?.Invoke(this, args);
                            UpdateMs = milliseconds;
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
                            Thread.Sleep((int)(wait - delay));
                        }
                        else
                        {
                            Thread.SpinWait((int)(wait - delay));
                        }
                        tick = sw.ElapsedTicks;
                        milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
                        delay = milliseconds - RenderMs;
                    }
                    if (delay >= wait)
                    {
                        var args = new OnRenderArgs { delay = delay };
                        OnRender?.Invoke(this, args);
                        RenderMs = milliseconds;
                        RendererContext();
                        Dev.SwapBuffers();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Renderer]{ex}");
                }
                //Thread.Sleep(1);
            }
            render();
            //Thread.Sleep(1);
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
        _newWidth = (uint)TargetWindow.Width;
        _newHeight = (uint)TargetWindow.Height;
        Root.UpdateScreenSize(TargetWindow.Width, TargetWindow.Height);
    }

    public Action RendererContext { get; init; } = () => { };

    public void Dispose()
    {
        MainThread?.Interrupt();
        RendererClass = null;
        TargetWindow?.Close();
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
