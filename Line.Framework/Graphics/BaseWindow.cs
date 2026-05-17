using System.Diagnostics;
using Line.Framework.Input;
using Line.Framework.UI;
using SharpGen.Runtime;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using UIScreen = Line.Framework.UI.UIScreen;

#nullable enable

namespace Line.Framework.Graphics;

public class BaseWindow:IDisposable
{
    public Sdl2Window TargetWindow { get; init; }
    public InputManager Input { get; init; }
    public GraphicsDevice Dev { get; init; }
    public UIScreen Root { get; } = new(0, 0);
    private Thread MainThread;
    public float FramePerSecond = 10;
    public float UpdatePerSecond = 1000;
    public CommandList commandList { get; init; }
    public UIDrawCollector Collector { get; init; } = new();

    //更新事件💩
    public class OnRenderArgs : EventArgs
    {
        public double delay;
    }

    public event EventHandler<OnRenderArgs>? OnRender;

    public class OnUpdateArgs : EventArgs
    {
        public double delay;
    }

    public event EventHandler<OnUpdateArgs>? OnUpdate;

    public static GraphicsBackend BackendSelector()
    {
        //建个队列（简单的不会搞）😭
        GraphicsBackend[] queue = new[]
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
        };
        Dev = VeldridStartup.CreateGraphicsDevice(TargetWindow, Options, (GraphicsBackend)Backend);
        //指令
        commandList = Dev.ResourceFactory.CreateCommandList();
        Collector = new();
        RendererContext = () =>
        {
            WindowsRenderer.UIRenderer(this, Collector);
        };
        TargetWindow.Resized += OnWindowResized;
        Root.UpdateScreenSize(TargetWindow.Width, TargetWindow.Height);

        //输入器
        Input = new(TargetWindow);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
    }

    private void UpdateWindow()
    {
        var sw = new Stopwatch();
        sw.Start();
        long tick = sw.ElapsedTicks;
        double milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
        double RenderMs = 0;
        double UpdateMs = 0;
        //开始考试
        while (TargetWindow.Exists)
        {
            tick = sw.ElapsedTicks;
            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
            //防止冻结
            if (UpdatePerSecond <= 0)
            {
                UpdatePerSecond = 1;
            }
            if (FramePerSecond <= 0)
            {
                FramePerSecond = 1;
            }

            //输入更新
            while (milliseconds - UpdateMs >= 1000d / UpdatePerSecond)
            {
                var args = new OnUpdateArgs { delay = milliseconds - UpdateMs };
                OnUpdate?.Invoke(this, args);
                UpdateMs += 1000d / UpdatePerSecond;
                TargetWindow.PumpEvents();
            }
            //处理大小更新
            if (_resizePending)
            {
                Dev.MainSwapchain.Resize(_newWidth, _newHeight);
                _resizePending = false;
            }

            //正式渲染
            try
            {
                while (milliseconds - RenderMs >= 1000d / FramePerSecond)
                {
                    var args = new OnRenderArgs { delay = milliseconds - RenderMs };
                    OnRender?.Invoke(this, args);
                    RenderMs += 1000d / FramePerSecond;
                    RendererContext();
                    Dev.SwapBuffers();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Renderer]{ex}");
            }
        }
    }

    private bool _resizePending = false;
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
        MainThread.Interrupt();
        TargetWindow.Close();
    }
}
