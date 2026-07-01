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
using Veldrid.StartupUtilities;
using UIScreen = Line.Framework.UI.UIScreen;

namespace Line.Framework.Graphics;

public class BaseWindow : IDisposable
{
    private unsafe nint _sdlRenderer;
    private unsafe nint _sdlTexture;
    internal Texture _stagingTexture;
    private int _currentBufferIndex = 0;
    private uint _width,
        _height;
    public WindowsRenderer RendererClass { get; private set; }
    public nint WindowHandle { get; init; }
    public InputManager Input { get; init; }
    public GraphicsDevice Dev { get; init; }
    internal readonly List<(Framebuffer, Texture)> backBuffers = [];
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
        _width = (uint)Width;
        _height = (uint)Height;
        WindowCreateInfo CreateInfo = new WindowCreateInfo(X, Y, Width, Height, State, Title);
        //一个窗口
        SDL.Init(SDL.InitFlags.Video);

        SDL.WindowFlags flags = SDL.WindowFlags.Vulkan | SDL.WindowFlags.Resizable;

        WindowHandle = SDL.CreateWindow(Title, Width, Height, flags);
        Width=(int)Size.X;
        Height=(int)Size.Y;
        SDL.ShowWindow(WindowHandle);
        SDL.SetHint("SDL_HINT_TOUCH_MOUSE_EVENTS", "0");
        WindowID = SDL.GetWindowID(WindowHandle);
        GraphicsDeviceOptions Options = new GraphicsDeviceOptions
        {
            //自动otto
            Debug = false,
            PreferStandardClipSpaceYDirection = true,
        };
        Dev = GraphicsDevice.CreateVulkan(Options);
        Texture texture1 = Dev.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)Width,
                (uint)Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled
            )
        );
        _stagingTexture = Dev.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)Width,
                (uint)Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Staging
            )
        );

        Texture texture2 = Dev.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)Width,
                (uint)Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled
            )
        );

        // 使用纹理创建两个 Framebuffer
        backBuffers.Add(
            new(
                Dev.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, texture1)),
                texture1
            )
        );

        backBuffers.Add(
            new(
                Dev.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, texture2)),
                texture2
            )
        );
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
        OnWindowResized();
        //输入器
        Input = new(this);
        MainThread = new Thread(UpdateWindow);
        MainThread.Start();
        MainThread.Name = "Renderer";

        //绑定事件

        EventPool.TryAdd(SDL.EventType.WindowResized, (a) =>
        {
            OnWindowResized();
        });
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
        _sdlRenderer = SDL.CreateRenderer(WindowHandle, null);
        if (_sdlRenderer == IntPtr.Zero)
        {
            Log.Error($"SDL 渲染器创建失败: {SDL.GetError()}");
            return;
        }
        _sdlTexture = SDL.CreateTexture(
            _sdlRenderer,
            SDL.PixelFormat.ABGR8888,
            SDL.TextureAccess.Streaming,
            (int)_width,
            (int)_height
        );
        if (_sdlTexture == IntPtr.Zero)
        {
            Log.Error($"SDL 纹理创建失败: {SDL.GetError()}");
            return;
        }

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
                                        return b.Window.WindowID != WindowHandle;
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
                Dev.WaitForIdle();
                SDL.DestroyTexture(_sdlTexture);
                _newWidth=(uint)Size.X;
                _newHeight=(uint)Size.Y;
                _sdlTexture = SDL.CreateTexture(
                    _sdlRenderer,
                    SDL.PixelFormat.ABGR8888,
                    SDL.TextureAccess.Streaming,
                    (int)_newWidth,
                    (int)_newHeight
                );
                _stagingTexture?.Dispose();
                _stagingTexture = Dev.ResourceFactory.CreateTexture(
                    TextureDescription.Texture2D(
                        _newWidth*1,
                        _newHeight,
                        1,
                        1,
                        PixelFormat.R8_G8_B8_A8_UNorm,
                        TextureUsage.Staging
                    )
                );
                for (int i = 0; i < backBuffers.Count; i++)
                {
                    backBuffers[i].Item1?.Dispose();
                    backBuffers[i].Item2?.Dispose();
                    Texture t = Dev.ResourceFactory.CreateTexture(
                        TextureDescription.Texture2D(
                            _newWidth*1,
                            _newHeight,
                            1,
                            1,
                            PixelFormat.R8_G8_B8_A8_UNorm,
                            TextureUsage.RenderTarget | TextureUsage.Sampled
                        )
                    );

                    backBuffers[i] = new(
                        Dev.ResourceFactory.CreateFramebuffer(new FramebufferDescription(null, t)),
                        t
                    );
                }
                RendererClass?.ReCreatePipeline(this);
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
                        Dev.WaitForIdle();

                        MappedResource map = Dev.Map(_stagingTexture, MapMode.Read);
                        try
                        {
                            unsafe
                            {
                                IntPtr pixelsPtr;
                                int pitch;
                                if (
                                    !SDL.LockTexture(
                                        _sdlTexture,
                                        IntPtr.Zero,
                                        out pixelsPtr,
                                        out pitch
                                    )
                                )
                                {
                                    Log.Warning($"SDL_LockTexture 失败: {SDL.GetError()}");
                                }
                                else
                                {
                                    byte* src = (byte*)map.Data;
                                    byte* dst = (byte*)pixelsPtr;
                                    int srcPitch = (int)map.RowPitch;
                                    SDL.GetTextureSize(_sdlTexture,out _,out float h);
                                    for (int i = 0; i < h; i++)
                                    {
                                        Buffer.MemoryCopy(src, dst, pitch, srcPitch);
                                        src += srcPitch;
                                        dst += pitch;
                                    }
                                    SDL.UnlockTexture(_sdlTexture);
                                }
                            }
                        }
                        finally
                        {
                            Dev.Unmap(_stagingTexture);
                        }

                        // 7. 通过 SDL 渲染器显示到窗口
                        SDL.RenderClear(_sdlRenderer);
                        SDL.RenderTexture(_sdlRenderer, _sdlTexture, IntPtr.Zero, IntPtr.Zero);
                        SDL.RenderPresent(_sdlRenderer);
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
        _newWidth = (uint)Size.X;
        _newHeight = (uint)Size.Y;
        Root.UpdateScreenSize((int)_newWidth,(int)_newHeight);
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
