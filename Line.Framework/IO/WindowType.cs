using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using Line.Framework.UI;
using SDL3;
using Veldrid;

namespace Line.Framework.IO;

public abstract class WindowType : IAsyncDisposable, IName
{
    protected static ConcurrentQueue<(SDL.Event Event, object[] Extra)> PollEvents { get; } = new();
    protected WindowType()
    {
        if (inited) return;
        inited = true;
        Entry.AddFunc(async _ =>
        {
            SDL.PumpEvents();
            List<object> obj = [];
            while (true)
            {
                var events = SDL.PollEvent(out var ev);
                if (!events) break;
                switch ((SDL.EventType)ev.Type)
                {
                    case SDL.EventType.TextInput:
                        obj.Add(Marshal.PtrToStringUTF8(ev.Text.Text));
                        break;
                }
                PollEvents.Enqueue(new(ev, obj.ToArray()));
            }
        }, -1);
    }
    private static bool inited = false;
    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Name => Title;
    /// <summary>
    /// 启用SDL事件输出
    /// </summary>
    public virtual bool EnableEventOutput { get; set; } = false;
    /// <summary>
    /// 是否允许鼠标在获得焦点时离开窗口
    /// </summary>
    public virtual bool AllowMouseLeave { get; set; } = true;

    /// <summary>
    /// 窗口SDL3句柄
    /// </summary>
    protected internal virtual nint WindowHandle { get; init; }

    /// <summary>
    /// 窗口输入管理器
    /// </summary>
    public virtual InputManager Input { get; init; }

    /// <summary>
    /// Veldrid图形设备
    /// </summary>
    public virtual GraphicsDevice Dev { get; init; }

    /// <summary>
    /// 请求退出时执行的Action
    /// </summary>
    public virtual Func<Task> RequestQuit { get; set; }

    /// <summary>
    /// 启用相对鼠标模式
    /// </summary>
    public virtual bool EnableMouseRelative
    {
        get;
        set
        {
            field = value;
            SDL.SetWindowRelativeMouseMode(WindowHandle, value);
            if (IsFocus)
            {
                if (ShowCursor)
                    SDL.ShowCursor();
                else
                    SDL.HideCursor();
            }
        }
    }

    /// <summary>
    /// 鼠标灵敏度(仅启用相对鼠标模式可用)
    /// </summary>
    public virtual float MouseSpeedScale
    {
        get;
        set
        {
            if (value > 0)
                field = value;
        }
    } = 1;

    /// <summary>
    /// 窗口大小
    /// </summary>
    public virtual Vector2 Size
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

    /// <summary>
    /// 窗口UI根节点
    /// </summary>
    public virtual UIScreen Root { get; init; }

    /// <summary>
    /// 垂直同步
    /// </summary>
    public virtual bool VSync
    {
        get;
        set
        {
            Dev?.SyncToVerticalBlank = value;
            field = value;
        }
    } = false;

    /// <summary>
    /// 渲染频率
    /// </summary>
    public virtual float FramePerSecond { get; set; } = 240;

    /// <summary>
    /// 启用全屏
    /// </summary>
    public virtual bool FullScreen
    {
        get;
        set
        {
            SDL.SetWindowFullscreen(WindowHandle, value);
            field = value;
        }
    } = false;

    /// <summary>
    /// 更新频率
    /// </summary>
    public virtual float UpdatePerSecond { get; set; } = 1000;

    /// <summary>
    /// 窗口内容缩放
    /// </summary>
    public virtual float Scale
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

    /// <summary>
    /// 窗口UI绘制收集器
    /// </summary>
    public virtual UIDrawCollector Collector { get; init; }

    /// <summary>
    /// 启用文本输入
    /// </summary>
    public virtual bool TextInput
    {
        get => SDL.TextInputActive(WindowHandle);
        set
        {
            if (value)
            {
                SDL.StartTextInput(WindowHandle);
                SDL.RaiseWindow(WindowHandle);
            }
            else
                SDL.StopTextInput(WindowHandle);
        }
    }

    /// <summary>
    /// 窗口渲染后端
    /// </summary>
    public virtual GraphicBackend RenderBackend { get; init; }

    /// <summary>
    /// 当渲染一帧时
    /// </summary>
    public event Action<double> OnRender;

    /// <summary>
    /// 渲染器
    /// </summary>
    public abstract RendererType Renderer { get; }

    /// <summary>
    /// 合成器
    /// </summary>
    public abstract ICompositor Compositor { get; }

    /// <summary>
    /// 当窗口更新时
    /// </summary>
    public event Action<double> OnUpdate;

    /// <summary>
    /// 后端选择器
    /// </summary>
    /// <returns>可用后端</returns>
    public static GraphicBackend BackendSelector()
    {
        //默认设备（到最后都用不了那就算了吧）
        int Choice = 0;
        for (int i = 0; i < 4; i++)
            if (IsBackendSupported((GraphicBackend)i))
            {
                Choice = i;
                break;
            }

        //代码死犟死犟的，就这样吧～
        return (GraphicBackend)Choice;
    }

    public static bool IsBackendSupported(GraphicBackend backend)
    {
        GraphicsBackend tbackend;
        switch ((int)backend)
        {
            case 0:
                tbackend = GraphicsBackend.Metal;
                break;
            case 1:
                tbackend = GraphicsBackend.Direct3D11;
                break;
            case 2:
                tbackend = GraphicsBackend.Vulkan;
                break;
            case 3:
                tbackend = GraphicsBackend.OpenGL;
                break;
            default:
                tbackend = GraphicsBackend.Vulkan;
                break;
        }
        return GraphicsDevice.IsBackendSupported(tbackend);
    }

    /// <summary>
    /// 资产构建器:音频构建器
    /// </summary>
    public virtual TAudio Audio { get; protected set; }

    /// <summary>
    /// SDL3窗口ID
    /// </summary>
    public virtual uint WindowID { get; init; }

    /// <summary>
    /// 窗口是否为焦点
    /// </summary>
    public virtual bool IsFocus => SDL.GetKeyboardFocus() == WindowHandle;

    /// <summary>
    /// 是否显示光标
    /// </summary>
    public virtual bool ShowCursor
    {
        get => field && !EnableMouseRelative;
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
    protected internal virtual ConcurrentDictionary<SDL.EventType, Func<SDL.Event, object[], Task>> EventPool { get; } =
        new();

    /// <summary>
    /// 当窗口获得焦点时
    /// </summary>
    public virtual event Action FocusGained;

    //当窗口失去焦点时
    public virtual event Action FocusLost;

    /// <summary>
    /// 窗口是否存在
    /// </summary>
    public virtual bool Exists
    {
        get => SDL.GetWindowID(WindowHandle) != 0;
    }

    /// <summary>
    /// 窗口标题
    /// </summary>
    public virtual string Title
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
    protected virtual async Task Render(CancellationToken token)
    {
        Stopwatch sw = new();
        double last = 0;
        sw.Start();
        while (!token.IsCancellationRequested)
        {
            //处理
            Task invokeTask = null;
            try
            {
                if (_resizePending)
                {
                    Dev?.WaitForIdle();
                    _newWidth = (uint)Size.X;
                    _newHeight = (uint)Size.Y;
                    Dev?.MainSwapchain.Resize(_newWidth, _newHeight);
                    _resizePending = false;
                    Dev?.SyncToVerticalBlank = VSync;
                }
                await RendererContext();
                Dev?.SwapBuffers();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            //休眠
            double waitTime = 0;
            if (FramePerSecond > 0) waitTime = 1000d / FramePerSecond;
            double ofs = GetStopwatchMs(sw) - last;
            try
            {
                invokeTask = Task.Run(() => OnRender?.Invoke(Math.Max(waitTime, ofs)), token);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, waitTime - ofs)), token);
                await invokeTask;
            }
            catch (TaskCanceledException)
            {/*_*/return; }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
            }
            last = GetStopwatchMs(sw);
        }
    }
    protected virtual async Task Update(CancellationToken token)
    {
        Stopwatch sw = new();
        double last = 0;
        sw.Start();
        Func<SDL.Event, bool> Filter = e => e.Window.WindowID == WindowID || e.TFinger.WindowID == WindowID;
        while (!token.IsCancellationRequested)
        {
            //处理
            if (IsFocus)
                SDL.SetHint(
                    SDL.Hints.MouseRelativeSpeedScale,
                    MouseSpeedScale.ToString()
                );
            int count = PollEvents.Count;
            for (int i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) return;
                if (!PollEvents.TryDequeue(out var ev)) break;
                if (!Filter(ev.Event))
                {
                    if (!Equals(ev, default))
                        PollEvents.Enqueue(ev);
                    continue;
                }
                foreach (var item in EventPool)
                {
                    if (token.IsCancellationRequested) return;
                    if (ev.Event.Type == (uint)SDL.EventType.WindowCloseRequested)
                    {
                        try
                        {
                            if (RequestQuit != null)
                                await RequestQuit.Invoke();
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"{ex}");
                        }
                    }
                    if (ev.Event.Type == (uint)item.Key)
                    {
                        try
                        {
                            if (item.Value != null)
                                await item.Value.Invoke(ev.Event, ev.Extra);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    }
                }

                if (EnableEventOutput) Log.Debug($"Event:{((SDL.EventType)ev.Event.Type).ToString()}");
            }
            if (
                AllowMouseLeave &&
                (0 == Input.Mouse.Position.X * Input.Mouse.Position.Y ||
            Size.X <= Input.Mouse.Position.X ||
            Size.Y <= Input.Mouse.Position.Y)
            )
                SDL.SetWindowRelativeMouseMode(WindowHandle, false);

            //休眠
            double waitTime = 0;
            if (UpdatePerSecond > 0) waitTime = 1000d / UpdatePerSecond;
            double ofs = GetStopwatchMs(sw) - last;
            try
            {
                var invokeTask = Task.Run(() => OnUpdate?.Invoke(Math.Max(waitTime, ofs)), token);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, waitTime - ofs)), token);
                await invokeTask;
            }
            catch (TaskCanceledException)
            {/*_*/return; }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
            }
            last = GetStopwatchMs(sw);
        }
    }
    public static double GetStopwatchMs(Stopwatch stopwatch)
    {
        if (stopwatch == null) return 0;
        return (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000.0;
    }
    protected abstract Task UpdateTask { get; }
    protected abstract Task RenderTask { get; }
    private bool _resizePending = false;
    protected virtual CancellationTokenSource TokenSource { get; set; } = new();

    /// <summary>
    /// 窗口资产管理器
    /// </summary>
    public virtual ResourceManager Resource { get; init; }
    private uint _newWidth,
        _newHeight;

    protected virtual void OnWindowResized()
    {
        _resizePending = true;
        SDL.GetWindowSize(WindowHandle, out _, out _);
        _newWidth = (uint)Size.X;
        _newHeight = (uint)Size.Y;
        Root.UpdateScreenSize((int)_newWidth, (int)_newHeight);
    }

    public virtual async Task RendererContext()
    {
        if (this != null && Collector != null)
        {
            if (Compositor == null || Renderer == null)
                return;
            var cp = await Compositor?.Composite(Root);
            if (cp != null)
                Renderer?.Render(cp);
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        await TokenSource.CancelAsync();
        try
        {
            Resource?.Dispose();
            Root?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }

        Renderer?.Dispose();
        try
        {
            Dev?.WaitForIdle();
            Dev?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning($"{ex.Message}");
        }
        if (WindowHandle != IntPtr.Zero) SDL.DestroyWindow(WindowHandle);
    }

    /// <summary>
    /// 获取全屏模式
    /// </summary>
    /// <param name="显示器ID"></param>
    /// <returns>全屏模式数组</returns>
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

    protected virtual void BindEvents()
    {
        //绑定事件
        EventPool.TryAdd(
            SDL.EventType.WindowResized,
            async (a, _) =>
            {
                OnWindowResized();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusGained,
            async (a, _) =>
            {
                SDL.RaiseWindow(WindowHandle);
                SDL.ShowWindow(WindowHandle);
                if (SDL.GetMouseFocus() == WindowHandle)
                {
                    if (ShowCursor)
                        SDL.ShowCursor();
                    else
                        SDL.HideCursor();
                    SDL.SetWindowRelativeMouseMode(WindowHandle, EnableMouseRelative);
                }
                FocusGained?.Invoke();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowMouseEnter,
            async (a, _) =>
            {
                SDL.SetWindowRelativeMouseMode(WindowHandle, EnableMouseRelative && IsFocus);
                if (ShowCursor)
                    SDL.ShowCursor();
                else
                    SDL.HideCursor();
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowMouseLeave,
            async (a, _) =>
            {
                SDL.SetWindowRelativeMouseMode(WindowHandle, false);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowRestored,
            async (a, _) =>
            {
                SDL.RaiseWindow(WindowHandle);
                SDL.ShowWindow(WindowHandle);
            }
        );
        EventPool.TryAdd(
            SDL.EventType.WindowFocusLost,
            async (a, _) =>
            {
                SDL.SetWindowRelativeMouseMode(WindowHandle, false);
                FocusLost?.Invoke();
            }
        );
        RequestQuit = async () => await DisposeAsync();
    }

    protected virtual void CreateResource()
    {
        Audio = new TAudio(Resource);
        Resource.AddType("Audio", Audio);
        Resource.AddType("Image", new TResourceSet(Dev, Renderer.TextureLayout));
        Resource.AddType("Font", new TFont(Dev, Renderer.TextureLayout));
    }
}
