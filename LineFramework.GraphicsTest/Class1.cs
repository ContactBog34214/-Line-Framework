using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Line.Framework;
using Line.Framework.Default.Graphics.SDL3;
using Line.Framework.Default.Graphics.Veldrid;
using Line.Framework.Graphics;
using SDL3;

namespace LineFramework.GraphicsTest;

public static class GT
{
    /// <summary>
    /// 切换图形后端。false = SDL3，true = Veldrid。
    /// <para>
    /// 注意：Veldrid 后端的 <c>IResourceFactory.CreateShader</c> 目前走的是
    /// Veldrid.SPIRV 的单参重载，那个重载是 compute 专用，
    /// 传顶点/片元着色器会在创建阶段失败。
    /// </para>
    /// </summary>
    private static readonly bool UseVeldridBackend = false;

    /// <summary>默认 Vulkan。</summary>
    private const GraphicBackend Backend = GraphicBackend.Vulkan;

    private const int InitialWidth = 1280;
    private const int InitialHeight = 720;

    /// <summary>
    /// 顶点着色器。只做 NDC 直通。
    /// <para>
    /// 3D 变换、透视投影、深度排序全部在 CPU 端完成，
    /// 因为抽象层目前没有 UBO 绑定和深度缓冲支持。
    /// </para>
    /// </summary>
    public const string VertexCode =
        @"
#version 450
layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}";

    public const string FragmentCode =
        @"
#version 450
layout(location = 0) in vec4 fsin_Color;

layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

    /// <summary>每个顶点：vec2 投影后位置 + vec4 颜色 = 24 字节。</summary>
    private const uint VertexStride = 24;

    /// <summary>6 个面 × 2 个三角形 × 3 个顶点。</summary>
    private const int VertexCount = 36;

    public static async Task Main()
    {
        Log.SetMinLevel(LogLevel.Warning);
        Log.EnableConsole(true);
        Log.SetLogFile(null);

        // ---- SDL 初始化 ---------------------------------------------------
        // SDL_WasInit 返回「已初始化子系统的掩码」，不是 bool。
        if ((SDL.WasInit(SDL.InitFlags.Video) & SDL.InitFlags.Video) == 0)
        {
            // 留空表示让 SDL 自选（X11 / Wayland 都行），
            // 想强制指定可改成 "x11" 或 "wayland"。
            if (!SDL.Init(SDL.InitFlags.Video))
                throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        }

        var window = SDL.CreateWindow(
            "Rotating Cube",
            InitialWidth,
            InitialHeight,
            SDL.WindowFlags.Resizable
        );
        if (window == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");

        SDL.ShowWindow(window);
        for (int i = 0; i < 10; i++)
        {
            SDL.PumpEvents();
            Thread.Sleep(10);
        }

        // ---- 诊断输出 -----------------------------------------------------
        // 关键：区分「逻辑尺寸」和「像素尺寸」。
        // Vulkan swapchain 需要的是像素尺寸；HiDPI 下两者不同，
        // 传错会导致 surface 尺寸与窗口不匹配，present 静默失败。
        SDL.GetWindowSize(window, out int logicalW, out int logicalH);
        int pixelW = logicalW,
            pixelH = logicalH;
        if (!SDL.GetWindowSizeInPixels(window, out pixelW, out pixelH))
        {
            pixelW = logicalW;
            pixelH = logicalH;
            Console.WriteLine($"GetWindowSizeInPixels failed: {SDL.GetError()}");
        }

        var driverName = SDL.GetCurrentVideoDriver();
        Console.WriteLine(
            $"[init] driver={driverName} window=0x{window:X} "
                + $"flags=0x{(ulong)SDL.GetWindowFlags(window):X}"
        );
        Console.WriteLine(
            $"[init] logical={logicalW}x{logicalH} pixel={pixelW}x{pixelH} "
                + $"scale={(logicalW > 0 ? (float)pixelW / logicalW : 1f):F2}"
        );

        // ---- 交换链参数 ---------------------------------------------------
        // 两个后端对 SwapchainSource 的解释不同：
        //   SDL3   → Surface 存 SDL_Window*
        //   Veldrid→ Surface/Connection 存原生窗口句柄
        SwapchainSource source;
        if (UseVeldridBackend)
        {
            source = CreateVeldridSwapchainSource(window, driverName);
        }
        else
        {
            source = new() { Kind = NativeWindowKind.Xlib, Surface = window };
        }

        Console.WriteLine(
            $"[swapchain] kind={source.Kind} "
                + $"surface=0x{source.Surface:X} connection=0x{source.Connection:X} "
                + $"size={pixelW}x{pixelH}"
        );

        var swapchainInfo = new SwapchainCreateInfo
        {
            Source = source,
            // 用像素尺寸，不是逻辑尺寸
            Width = (uint)pixelW,
            Height = (uint)pixelH,
            SyncToVerticalBlank = false,
        };

        // ---- 设备 + 交换链 ------------------------------------------------
        // Veldrid 在创建设备时一并建立第一个交换链，通过 out 返回；
        // 框架不区分「主」交换链，这个就是普通的一个，由 using 释放。
        // SDL3 则先建设备，再用同一个 createInfo 建交换链。
        IGraphicsDevice dev;
        ISwapchain chain;
        if (UseVeldridBackend)
        {
            dev = new VeldridDevice(Backend, swapchainInfo, out chain);
        }
        else
        {
            dev = new SDL3Device(Backend);
            chain = dev.ResourceFactory.CreateSwapchain(swapchainInfo);
        }

        Console.WriteLine($"[device] {dev.Name}");
        Console.WriteLine(
            $"[frame]  framebuffer={chain.FrameBuffer.OutputFormat.ColorAttachments?.Length ?? 0} attach"
        );

        using (dev)
        using (chain)
        {
            RunLoop(dev, chain, window);
        }

        SDL.DestroyWindow(window);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 按实际的视频驱动构造 <see cref="SwapchainSource"/>。
    /// <para>
    /// X11 与 Wayland 的原生句柄完全不同，用错必然无法呈现：
    /// X11 要 <c>Window</c> + <c>Display*</c>，
    /// Wayland 要 <c>wl_surface*</c> + <c>wl_display*</c>。
    /// </para>
    /// </summary>
    private static SwapchainSource CreateVeldridSwapchainSource(IntPtr window, string videoDriver)
    {
        var props = SDL.GetWindowProperties(window);

        if (videoDriver is "wayland")
        {
            return new()
            {
                Kind = NativeWindowKind.Wayland,
                Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandSurfacePointer,
                    IntPtr.Zero
                ),
                Connection = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandDisplayPointer,
                    IntPtr.Zero
                ),
            };
        }

        // 默认按 X11 处理（SDL 的 x11 驱动，以及 xwayland 下都是 X11 句柄）
        return new()
        {
            Kind = NativeWindowKind.Xlib,
            Surface = (IntPtr)SDL.GetNumberProperty(props, SDL.Props.WindowX11WindowNumber, 0),
            Connection = SDL.GetPointerProperty(
                props,
                SDL.Props.WindowX11DisplayPointer,
                IntPtr.Zero
            ),
        };
    }

    /// <summary>
    /// 渲染主循环。与设备 / 交换链的具体后端无关。
    /// </summary>
    private static void RunLoop(IGraphicsDevice dev, ISwapchain chain, IntPtr window)
    {
        // ---- 着色器 -------------------------------------------------------
        var vs = dev.ResourceFactory.CreateShader(
            new()
            {
                Stage = ShaderStage.Vertex,
                Bytes = Encoding.UTF8.GetBytes(VertexCode),
                EntryPoint = "main",
            }
        );
        var fs = dev.ResourceFactory.CreateShader(
            new()
            {
                Stage = ShaderStage.Fragment,
                Bytes = Encoding.UTF8.GetBytes(FragmentCode),
                EntryPoint = "main",
            }
        );

        // ---- 顶点缓冲 -----------------------------------------------------
        using var vb = dev.ResourceFactory.CreateBuffer(
            new BufferCreateInfo
            {
                SizeInBytes = VertexCount * VertexStride,
                Usage = BufferUsage.Vertex | BufferUsage.Dynamic,
            }
        );

        var layout = new VertexLayoutDescription
        {
            Stride = VertexStride,
            Attributes =
            [
                new() { Format = VertexFormat.Float2, Offset = 0 },
                new() { Format = VertexFormat.Float4, Offset = 8 },
            ],
        };

        // ---- 管线 ---------------------------------------------------------
        var description = new GraphicsDescription
        {
            RasterizerState = new()
            {
                CullMode = FaceCullMode.None,
                FillMode = PolygonFillMode.Solid,
                FrontFace = FrontFace.Clockwise,
                DepthClipEnabled = true,
                ScissorTestEnabled = false,
            },
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ShaderSet = new() { Shaders = [vs, fs], VertexLayouts = [layout] },
            BlendState = BlendStateDescription.SingleAlphaBlend,
            Outputs = chain.FrameBuffer.OutputFormat,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            FlipY = true,
        };

        using var pipeline = dev.ResourceFactory.CreatePipeline(
            new() { Mode = description, ResourceLayouts = [] }
        );

        // ---- 立方体几何 ---------------------------------------------------
        // 半边长为 1 的立方体，8 个顶点
        Vector3[] cubeCorners =
        [
            new(-1, -1, -1), // 0
            new(1, -1, -1), // 1
            new(1, 1, -1), // 2
            new(-1, 1, -1), // 3
            new(-1, -1, 1), // 4
            new(1, -1, 1), // 5
            new(1, 1, 1), // 6
            new(-1, 1, 1), // 7
        ];

        // 6 个面，每面 2 个三角形（按外表面逆时针排列）
        int[][] faces =
        [
            [4, 5, 6, 4, 6, 7], // 前 (z = +1)
            [1, 0, 3, 1, 3, 2], // 后 (z = -1)
            [0, 4, 7, 0, 7, 3], // 左 (x = -1)
            [5, 1, 2, 5, 2, 6], // 右 (x = +1)
            [3, 7, 6, 3, 6, 2], // 上 (y = +1)
            [0, 1, 5, 0, 5, 4], // 下 (y = -1)
        ];

        // 每面一个颜色，便于观察旋转
        (float R, float G, float B, float A)[] faceColors =
        [
            (1.00f, 0.30f, 0.30f, 1f), // 前 红
            (0.30f, 0.50f, 1.00f, 1f), // 后 蓝
            (0.30f, 1.00f, 0.40f, 1f), // 左 绿
            (1.00f, 0.85f, 0.25f, 1f), // 右 黄
            (0.75f, 0.40f, 1.00f, 1f), // 上 紫
            (0.25f, 0.90f, 0.95f, 1f), // 下 青
        ];

        // ---- 相机参数 -----------------------------------------------------
        const float CameraDistance = 4.5f; // 相机到原点距离
        const float FocalLength = 2.5f; // 焦距，决定视场大小

        float[] verts = new float[VertexCount * 6];
        Vector3[] rotated = new Vector3[8];
        Vector2[] projected = new Vector2[8];
        float[] depthOfCorner = new float[8];

        var stopwatch = Stopwatch.StartNew();
        int lastWidth = InitialWidth;
        int lastHeight = InitialHeight;
        bool running = true;
        bool reportedSwapError = false;

        Console.WriteLine("Close the window (or Alt+F4) to exit.");

        while (running)
        {
            SDL.PumpEvents();

            // 只响应窗口关闭，按键不退出。
            while (SDL.PollEvent(out var ev))
            {
                var type = (SDL.EventType)ev.Type;
                if (type is SDL.EventType.Quit or SDL.EventType.WindowCloseRequested)
                    running = false;
            }

            if (!running)
                break;

            // ---- 响应窗口缩放 ---------------------------------------------
            // 注意：swapchain 用的是「像素尺寸」，不是逻辑尺寸。
            // HiDPI 下两者不同，混用会让 surface 尺寸与窗口不匹配。
            SDL.GetWindowSize(window, out int winW, out int winH);
            int pixW = winW,
                pixH = winH;
            if (!SDL.GetWindowSizeInPixels(window, out pixW, out pixH))
            {
                pixW = winW;
                pixH = winH;
            }

            if (pixW != lastWidth || pixH != lastHeight)
            {
                lastWidth = pixW;
                lastHeight = pixH;

                if (pixW > 0 && pixH > 0)
                {
                    // 交换链必须与窗口的像素尺寸保持一致。
                    // Veldrid 的 SwapBuffers 在尺寸不匹配时 present 会失败，
                    // 表现为窗口始终不更新（看起来像"没有窗口"）。
                    // 平铺式窗口管理器可能在创建后立刻改变窗口尺寸，
                    // 所以这一处不能省。
                    dev.WaitForIdle();
                    chain.Resize(pixW, pixH);
                }

                Console.WriteLine($"Window resized: logical={winW}x{winH} pixel={pixW}x{pixH}");
            }

            if (pixW <= 0 || pixH <= 0)
            {
                Thread.Sleep(16);
                continue;
            }

            float aspect = (float)pixW / pixH;

            // ---- 3D 变换（CPU 端）-----------------------------------------
            float t = (float)stopwatch.Elapsed.TotalSeconds;
            float ax = t * 0.7f;
            float ay = t * 1.1f;

            float cosX = MathF.Cos(ax),
                sinX = MathF.Sin(ax);
            float cosY = MathF.Cos(ay),
                sinY = MathF.Sin(ay);

            for (int i = 0; i < 8; i++)
            {
                var c = cubeCorners[i];

                // 先绕 Y 轴，再绕 X 轴
                float x1 = c.X * cosY + c.Z * sinY;
                float z1 = -c.X * sinY + c.Z * cosY;

                float y2 = c.Y * cosX - z1 * sinX;
                float z2 = c.Y * sinX + z1 * cosX;

                rotated[i] = new Vector3(x1, y2, z2);

                // 相机在 +Z 方向、看向原点，所以相机空间深度 = 距离 + z
                // （旋转后 z 越大表示越靠近观察者）
                float zCam = CameraDistance - z2;
                depthOfCorner[i] = zCam;

                // 透视投影：越远越小
                float invZ = 1f / zCam;
                projected[i] = new Vector2(
                    FocalLength * x1 * invZ / aspect, // 宽高比校正
                    FocalLength * y2 * invZ
                );
            }

            // ---- 按面深度排序（画家算法，远 → 近）------------------------
            // 没有深度缓冲，只能靠提交顺序表达遮挡关系。
            // 对凸多面体，按面平均深度排序即可正确遮挡。
            Span<int> faceOrder = stackalloc int[6];
            Span<float> faceDepth = stackalloc float[6];

            for (int f = 0; f < 6; f++)
            {
                var idx = faces[f];
                // 用每个面 6 个顶点的平均深度
                float sum = 0;
                for (int k = 0; k < 6; k++)
                    sum += depthOfCorner[idx[k]];
                faceDepth[f] = sum / 6f;
                faceOrder[f] = f;
            }

            // 插入排序，按深度降序（远的先画）
            for (int i = 1; i < 6; i++)
            {
                int key = faceOrder[i];
                float keyDepth = faceDepth[key];
                int j = i - 1;
                while (j >= 0 && faceDepth[faceOrder[j]] < keyDepth)
                {
                    faceOrder[j + 1] = faceOrder[j];
                    j--;
                }
                faceOrder[j + 1] = key;
            }

            // ---- 生成顶点数据 ---------------------------------------------
            int cursor = 0;
            for (int fi = 0; fi < 6; fi++)
            {
                int f = faceOrder[fi];
                var idx = faces[f];
                var col = faceColors[f];

                for (int k = 0; k < 6; k++)
                {
                    var p = projected[idx[k]];
                    int o = cursor * 6;

                    verts[o + 0] = p.X;
                    verts[o + 1] = p.Y;
                    verts[o + 2] = col.R;
                    verts[o + 3] = col.G;
                    verts[o + 4] = col.B;
                    verts[o + 5] = col.A;

                    cursor++;
                }
            }

            vb.Update(0, MemoryMarshal.AsBytes(verts.AsSpan()));

            // ---- 录制并提交 -----------------------------------------------
            using var cl = dev.ResourceFactory.CreateCommandList();
            using (cl.Record())
            {
                cl.SetFrameBuffer(chain.FrameBuffer);
                cl.ClearColorTarget(0, new(0.06f, 0.06f, 0.08f, 1f));
                cl.SetPipeline(pipeline);
                cl.SetVertexBuffer(0, vb);
                cl.Draw(VertexCount, 1, 0, 0);
            }
            dev.SubmitCommands(cl);

            // 呈现。Veldrid 在交换链尺寸与窗口不匹配会在这里抛异常，
            // 捕获后打印一次，避免"静默黑屏"。
            try
            {
                chain.Swap();
            }
            catch (Exception ex)
            {
                if (!reportedSwapError)
                {
                    reportedSwapError = true;
                    Console.WriteLine($"[swap] FAILED: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Thread.Sleep(16);
        }

        dev.WaitForIdle();
    }
}
