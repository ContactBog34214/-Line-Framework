using System.Diagnostics;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

#nullable enable

namespace Line.Framework.Graphics;

public class BaseWindow
{
    public Sdl2Window TargetWindow { get; init; }
    public GraphicsDevice Dev { get; init; }
    private Thread MainThread;
    public int UpdatePerSecond = 1;
    public CommandList commandList { get; init; }

    //更新事件💩
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
        RendererContext = (() => { });
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
        RendererContext = () =>
        {
            WindowsRenderer.UIRenderer(this);
        };
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
    }

    private void UpdateWindow()
    {
        var sw = new Stopwatch();
        sw.Start();
        long tick = sw.ElapsedTicks;
        //开始考试
        while (TargetWindow.Exists)
        {
            TargetWindow.PumpEvents();
            try
            {
                RendererContext();
            }
            catch (Exception ex)
            {
                Log.Error($"[Renderer]{ex}");
            }
            Dev.SwapBuffers();
            long eTick = sw.ElapsedTicks;
            double elapsedMs = (eTick - tick) * 1000.0 / Stopwatch.Frequency;
            double delay = elapsedMs;
            if (UpdatePerSecond <= 0)
            {
                UpdatePerSecond = 1;
            }
            double targetSleepMs = 1000.0 / UpdatePerSecond;
            if (elapsedMs < targetSleepMs)
            {
                //强制睡眠
                delay = targetSleepMs;
                var args = new OnUpdateArgs { delay = delay };
                OnUpdate?.Invoke(this, args);
                Thread.Sleep((int)(targetSleepMs - elapsedMs));
                eTick = sw.ElapsedTicks;
                elapsedMs = (eTick - tick) * 1000.0 / Stopwatch.Frequency;
            }
            else
            {
                //杂鱼
                var args = new OnUpdateArgs { delay = delay };
                OnUpdate?.Invoke(this, args);
            }
            tick = sw.ElapsedTicks;
        }
    }

    public Action RendererContext { get; init; }
}
