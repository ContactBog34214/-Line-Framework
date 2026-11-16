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
    /// 两个后端现在吃同一份 GLSL 源码：SDL3 在工厂内编 SPIR-V，
    /// Veldrid 也先编 SPIR-V 再创建（Vulkan 后端直接用它）。
    /// </para>
    /// </summary>
    private static readonly bool UseVeldridBackend = true;
    private static readonly int alpha = 255;

    /// <summary>
    /// true = 跑 UBO 版（静态顶点缓冲 + 每帧 80 字节 uniform），
    /// false = 跑原来的 CPU 变换版。
    /// <para>
    /// 两个示例共用同一套设备 / 交换链初始化，只换渲染内容，
    /// 便于对照「UBO 到底把哪些工作移到了 GPU」。
    /// </para>
    /// </summary>
    private const bool UseUniformBufferDemo = true;

    /// <summary>true = 打印 Debug 级诊断（着色器资源计数、资源集首绑信息）。</summary>
    private const bool VerboseLog = true;

    /// <summary>
    /// true = 用探针版顶点着色器：位置硬编码、颜色只反映 UBO 的内容。
    /// <para>
    /// 纯灰方块 = uniform 读到的全是 0；偏黄白且有脉动 = 链路正常。
    /// 排查 uniform 投递时临时打开，正常渲染请保持 false。
    /// </para>
    /// </summary>
    private const bool UniformProbeMode = false;

    /// <summary>默认 Vulkan。</summary>
    private const GraphicBackend Backend = GraphicBackend.Vulkan;

    private const int InitialWidth = 1280;
    private const int InitialHeight = 720;

    /// <summary>
    /// 顶点着色器。只做 NDC 直通。
    /// <para>
    /// 这是 <see cref="RunLoop"/>（CPU 变换版）用的着色器：
    /// 3D 变换、透视投影、深度排序全部在 CPU 端完成。
    /// UBO 版用 <see cref="UniformVertexCode"/>，变换挪到了 GPU。
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
        // Debug 级别会打出每个着色器统计到的资源数量、以及资源集首绑的槽位信息。
        // 排查「窗口开着但不渲染」时先打开它。
        Log.SetMinLevel(VerboseLog ? LogLevel.Debug : LogLevel.Warning);
        Log.EnableConsole(true);
        Log.SetLogFile(null);

        // ---- SDL 初始化 ---------------------------------------------------
        // SDL_WasInit 返回「已初始化子系统的掩码」，不是 bool。
        if ((SDL.WasInit(SDL.InitFlags.Video) & SDL.InitFlags.Video) == 0)
        {
            // 视频驱动必须在 SDL_Init 之前选好。
            //
            // SDL3 GPU 在 Wayland 下拿不到可用的交换链纹理
            //（SDL_WaitAndAcquireGPUSwapchainTexture 返回 0），
            // 表现为「窗口开着、画面不动、也不报错」。
            // Veldrid 走的是自己的 wl_surface 路径，不受影响，
            // 所以只在 SDL3 后端下强制 X11（经 XWayland）。
            if (OperatingSystem.IsLinux())
            {
                if (!UseVeldridBackend)
                    SDL.SetHint(SDL.Hints.VideoDriver, "x11");
                else if (
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                )
                    SDL.SetHint(SDL.Hints.VideoDriver, "wayland");
            }

            if (!SDL.Init(SDL.InitFlags.Video))
                throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        }

        SDL.WindowFlags windowFlag = SDL.WindowFlags.Resizable;
        if (alpha != 255 && UseVeldridBackend == true)
            windowFlag |= SDL.WindowFlags.Transparent;

        var window = SDL.CreateWindow("Rotating Cube", InitialWidth, InitialHeight, windowFlag);
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
            if (UseUniformBufferDemo)
                RunUniformCube(dev, chain, window);
            else
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
                cl.ClearColorTarget(0, new(0.06f, 0.06f, 0.08f, alpha / 255f));
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

    // =====================================================================
    //  UniformBuffer 示例
    // =====================================================================

    /// <summary>
    /// 顶点着色器：3D 变换改由 GPU 完成，矩阵来自 UBO。
    /// <para>
    /// <b>std140 与 C# 侧的对应关系</b>：块内 80 字节 = mat4 Mvp(0..63) + vec4 Tint(64..79)。
    /// 声明顺序必须与 <see cref="TransformUniform"/> 的字段顺序完全一致，
    /// 否则 Tint 会读到 Mvp 里，表现为颜色全错。
    /// </para>
    /// <para>
    /// <c>set = 0, binding = 0</c> 是<b>抽象层的通用约定</b>（与 Veldrid 的
    /// ResourceBindingModel.Default 一致）。SDL3 后端会在编译 GLSL 时
    /// 按阶段重写成它自己的约定（顶点 set 1 / 片元 set 3），
    /// 因此这份 shader 不需要为任何后端特化。
    /// </para>
    /// </summary>
    private const string UniformVertexCode =
        @"
#version 450
layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

layout(set = 0, binding = 0, std140) uniform Transform
{
    mat4 Mvp;
    vec4 Tint;
} ubo;

void main()
{
    gl_Position = ubo.Mvp * vec4(Position, 1.0);
    fsin_Color = Color * ubo.Tint;
}";

    /// <summary>
    /// uniform 探针版顶点着色器（阶段隔离实验用）。
    /// <para>
    /// 位置硬编码（保证一定有像素），**完全不读 uniform** ——
    /// 顶点阶段只把顶点色传给片元，uniform 的读取交给
    /// <see cref="ProbeFragmentCode"/>。
    /// 这样顶点 / 片元两条 uniform 路径可以被分开测。
    /// </para>
    /// </summary>
    private const string ProbeVertexCode =
        @"
#version 450
layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position.xy * 0.4, 0.5, 1.0);
    fsin_Color = Color;
}";

    /// <summary>
    /// uniform 探针版片元着色器：颜色只由 UBO 决定。
    /// <para>
    /// 读数全为 0 → 纯灰 (0.25, 0.25, 0.25)；
    /// 有数据 → 绿色通道按 Tint.x 脉动，红色通道按 Mvp[0][0] 常亮。
    /// </para>
    /// <para>
    /// 与顶点版配合使用，用来判断「顶点 uniform 路径」和「片元 uniform 路径」
    /// 是同时失效还是只有一边失效。
    /// </para>
    /// </summary>
    private const string ProbeFragmentCode =
        @"
#version 450
layout(location = 0) in vec4 fsin_Color;

layout(location = 0) out vec4 fsout_Color;

layout(set = 0, binding = 0, std140) uniform Transform
{
    mat4 Mvp;
    vec4 Tint;
} ubo;

void main()
{
    float a = clamp(abs(ubo.Mvp[0][0]) * 0.25, 0.0, 1.0);
    float b = clamp(abs(ubo.Tint.x) * 0.5, 0.0, 1.0);
    fsout_Color = vec4(0.25 + a * 0.75, 0.25 + b * 0.75, 0.25, 1.0);
}";

    private const string UniformFragmentCode =
        @"
#version 450
layout(location = 0) in vec4 fsin_Color;

layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

    /// <summary>
    /// UBO 的 C# 侧布局，必须与 shader 里的块逐字段对应。
    /// <para>
    /// <c>Matrix4x4</c> 在 .NET 里是行主序 + 行向量约定，
    /// 而 GLSL 按列主序解释同一块内存 —— 两者刚好互相抵消，
    /// 所以<b>直接上传、不要转置</b>。转置反而会得到错误的矩阵。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TransformUniform
    {
        /// <summary>offset 0，64 字节。</summary>
        public Matrix4x4 Mvp;

        /// <summary>offset 64，16 字节。</summary>
        public Vector4 Tint;

        /// <summary>std140 大小：80 字节（16 的倍数，无需尾部补齐）。</summary>
        public const uint SizeInBytes = 80;
    }

    /// <summary>
    /// UBO 版立方体。
    /// <para>
    /// 与 <see cref="RunLoop"/> 的差别：
    /// <list type="bullet">
    /// <item>顶点数据完全静态，开头上传一次，之后不再动</item>
    /// <item>每帧只更新 80 字节 UBO，CPU 不再重算 36 个投影后顶点</item>
    /// <item>遮挡关系靠「6 个面各一次 Draw + 按深度排序」，不需要重建缓冲</item>
    /// </list>
    /// </para>
    /// </summary>
    private static void RunUniformCube(IGraphicsDevice dev, ISwapchain chain, IntPtr window)
    {
        const float CameraDistance = 4.5f;
        const float FocalLength = 2.5f;
        const float Near = 0.1f;
        const float Far = 100f;
        const int VertsPerFace = 6;

        // ---- 资源布局 + UBO + 资源集 --------------------------------------
        // 释放顺序由 using 的逆序保证：resourceSet 先于 ubo，ubo 先于 layout。
        using var uboLayout = dev.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutCreateInfo
            {
                Elements =
                [
                    new()
                    {
                        Name = "Transform",
                        Kind = ResourceKind.UniformBuffer,
                        // 探针模式下片元着色器也要读同一个 UBO，
                        // 因此该元素必须同时声明两个阶段 ——
                        // 否则 SDL3ResourceSet 不会往片元槽位推送。
                        Stages = UniformProbeMode
                            ? ShaderStage.Vertex | ShaderStage.Fragment
                            : ShaderStage.Vertex,
                        SizeInBytes = TransformUniform.SizeInBytes,
                    },
                ],
            }
        );

        using var ubo = dev.ResourceFactory.CreateBuffer(
            new BufferCreateInfo
            {
                SizeInBytes = TransformUniform.SizeInBytes,
                // Uniform 标志在 Veldrid 侧是绑定校验的硬要求；
                // Dynamic 表示每帧重写（Veldrid 用可映射内存）。
                // SDL3 侧这一整块会变成 CPU 影子，不走 GPU 缓冲。
                Usage = BufferUsage.Uniform | BufferUsage.Dynamic,
            }
        );

        using var resourceSet = dev.ResourceFactory.CreateResourceSet(
            new ResourceSetCreateInfo
            {
                Layout = uboLayout,
                Bindings = [ResourceBinding.FromBuffer(ubo)],
            }
        );

        // ---- 着色器 -------------------------------------------------------
        // 两个后端吃同一份 GLSL：SDL3 自己编 SPIR-V，
        // Veldrid 的工厂内部编 SPIR-V 再交叉编译。
        using var vs = dev.ResourceFactory.CreateShader(
            new()
            {
                Stage = ShaderStage.Vertex,
                Bytes = Encoding.UTF8.GetBytes(
                    UniformProbeMode ? ProbeVertexCode : UniformVertexCode
                ),
                EntryPoint = "main",
                // SDL3 在创建着色器时就要知道用了几个 uniform，
                // 缺了这个布局它会把 NumUniformBuffers 记成 0。
                ResourceLayout = uboLayout,
            }
        );

        using var fs = dev.ResourceFactory.CreateShader(
            new()
            {
                Stage = ShaderStage.Fragment,
                Bytes = Encoding.UTF8.GetBytes(
                    UniformProbeMode ? ProbeFragmentCode : UniformFragmentCode
                ),
                EntryPoint = "main",
                // 探针模式的片元着色器也读 ubo，同样要声明资源，
                // 否则 SDL3 会把片元的 NumUniformBuffers 记成 0。
                ResourceLayout = UniformProbeMode ? uboLayout : null,
            }
        );

        // ---- 静态几何 -----------------------------------------------------
        Vector3[] cubeCorners =
        [
            new(-1, -1, -1),
            new(1, -1, -1),
            new(1, 1, -1),
            new(-1, 1, -1),
            new(-1, -1, 1),
            new(1, -1, 1),
            new(1, 1, 1),
            new(-1, 1, 1),
        ];

        int[][] faces =
        [
            [4, 5, 6, 4, 6, 7], // 前 (z = +1)
            [1, 0, 3, 1, 3, 2], // 后 (z = -1)
            [0, 4, 7, 0, 7, 3], // 左 (x = -1)
            [5, 1, 2, 5, 2, 6], // 右 (x = +1)
            [3, 7, 6, 3, 6, 2], // 上 (y = +1)
            [0, 1, 5, 0, 5, 4], // 下 (y = -1)
        ];

        (float R, float G, float B)[] faceColors =
        [
            (1.00f, 0.30f, 0.30f),
            (0.30f, 0.50f, 1.00f),
            (0.30f, 1.00f, 0.40f),
            (1.00f, 0.85f, 0.25f),
            (0.75f, 0.40f, 1.00f),
            (0.25f, 0.90f, 0.95f),
        ];

        // stride = 12(vec3) + 16(vec4) = 28
        var layout = new VertexLayoutDescription
        {
            Stride = 28,
            Attributes =
            [
                new() { Format = VertexFormat.Float3, Offset = 0 },
                new() { Format = VertexFormat.Float4, Offset = 12 },
            ],
        };

        using var pipeline = dev.ResourceFactory.CreatePipeline(
            new()
            {
                Mode = new GraphicsDescription
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
                },
                ResourceLayouts = [uboLayout],
            }
        );

        // 每面 6 个顶点，静态顶点缓冲
        float[] verts = new float[6 * VertsPerFace * 7];
        int c = 0;
        for (int f = 0; f < 6; f++)
        {
            var col = faceColors[f];
            for (int k = 0; k < VertsPerFace; k++)
            {
                var p = cubeCorners[faces[f][k]];
                verts[c++] = p.X;
                verts[c++] = p.Y;
                verts[c++] = p.Z;
                verts[c++] = col.R;
                verts[c++] = col.G;
                verts[c++] = col.B;
                verts[c++] = 1f;
            }
        }

        using var vb = dev.ResourceFactory.CreateBuffer(
            new BufferCreateInfo
            {
                SizeInBytes = (ulong)verts.Length * sizeof(float),
                // 只在下面上传一次，不需要 Dynamic
                Usage = BufferUsage.Vertex,
            }
        );
        vb.Update(0, MemoryMarshal.AsBytes(verts.AsSpan()));

        // ---- 每帧临时状态 -------------------------------------------------
        var uniformScratch = new TransformUniform[1];
        var cornerDepth = new float[8];
        var faceDepth = new float[6];
        Span<int> faceOrder = stackalloc int[6];

        var stopwatch = Stopwatch.StartNew();
        int lastWidth = InitialWidth,
            lastHeight = InitialHeight;
        bool running = true;

        Console.WriteLine("[uniform] static VB + per-frame UBO (80 bytes). Close to exit.");

        while (running)
        {
            SDL.PumpEvents();
            while (SDL.PollEvent(out var ev))
            {
                var type = (SDL.EventType)ev.Type;
                if (type is SDL.EventType.Quit or SDL.EventType.WindowCloseRequested)
                    running = false;
            }
            if (!running)
                break;

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
                    dev.WaitForIdle();
                    chain.Resize(pixW, pixH);
                }
            }

            if (pixW <= 0 || pixH <= 0)
            {
                Thread.Sleep(16);
                continue;
            }

            float aspect = (float)pixW / pixH;
            float t = (float)stopwatch.Elapsed.TotalSeconds;

            // ---- 矩阵（行向量约定：model * view * projection）-------------
            var model = Matrix4x4.CreateRotationY(t * 1.1f) * Matrix4x4.CreateRotationX(t * 0.7f);
            var view = Matrix4x4.CreateTranslation(0, 0, -CameraDistance);
            var projection = CreatePerspective(FocalLength, aspect, Near, Far);
            var modelView = model * view;

            // ---- 画家算法：只需要 8 个角点的相机空间深度 ------------------
            for (int i = 0; i < 8; i++)
            {
                // 相机看向 -Z，所以 -cam.Z 就是到相机的距离（越大越远）
                cornerDepth[i] = -Vector3.Transform(cubeCorners[i], modelView).Z;
            }

            for (int f = 0; f < 6; f++)
            {
                float sum = 0;
                for (int k = 0; k < VertsPerFace; k++)
                    sum += cornerDepth[faces[f][k]];
                faceDepth[f] = sum / VertsPerFace;
                faceOrder[f] = f;
            }

            // 远的先画（插入排序，6 个元素无所谓复杂度）
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

            // ---- 更新 UBO -------------------------------------------------
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 3f);
            uniformScratch[0].Mvp = modelView * projection;
            uniformScratch[0].Tint = new Vector4(pulse, pulse, pulse, alpha / 255f);

            // 注意顺序：先写数据，再绑定资源集。
            // SDL3 的 uniform 是「绑定即推送快照」，反过来本帧读到的还是上一帧。
            ubo.Update(0, MemoryMarshal.AsBytes(uniformScratch.AsSpan()));

            // ---- 录制并提交 -----------------------------------------------
            using var cl = dev.ResourceFactory.CreateCommandList();
            using (cl.Record())
            {
                // 资源集绑定放在最前面 —— 这个顺序对两个后端都成立，
                // 因为它们各自在内部把「绑定」延后到了合法时机：
                //
                //   Veldrid：要求「先有活跃管线才允许 SetGraphicsResourceSet」，
                //            因此它把管线之前的绑定登记下来，等 SetPipeline 后补绑。
                //   SDL3   ：uniform 走 PushGPU*UniformData，必须在渲染通道之外；
                //            sampler 又必须在通道之内。因此它立刻推送 uniform、
                //            把 sampler 登记下来，等通道开启时补绑。
                //
                // 结论：调用方可以在 SetPipeline 之前或之后绑定资源集，
                // 无需为任何后端特化顺序。
                cl.SetGraphicsResourceSet(0, resourceSet);

                cl.SetFrameBuffer(chain.FrameBuffer);
                cl.ClearColorTarget(0, new(0.06f, 0.06f, 0.08f, alpha / 255f));
                cl.SetPipeline(pipeline);
                cl.SetVertexBuffer(0, vb);

                for (int fi = 0; fi < 6; fi++)
                {
                    int face = faceOrder[fi];
                    cl.Draw(VertsPerFace, 1, (uint)(face * VertsPerFace), 0);
                }
            }
            dev.SubmitCommands(cl);
            chain.Swap();

            Thread.Sleep(16);
        }

        dev.WaitForIdle();
    }

    /// <summary>
    /// 透视投影，深度范围 [0, 1]（Vulkan 约定，Veldrid / SDL3 都是 Vulkan 路径）。
    /// <para>
    /// 行向量约定：<c>w' = -z_cam</c>（右手系、相机看向 -Z），
    /// 近平面映射到 z_ndc = 0，远平面到 1。
    /// </para>
    /// </summary>
    private static Matrix4x4 CreatePerspective(
        float focalLength,
        float aspect,
        float near,
        float far
    ) =>
        new(
            focalLength / aspect,
            0,
            0,
            0,
            0,
            focalLength,
            0,
            0,
            0,
            0,
            far / (near - far),
            -1,
            0,
            0,
            near * far / (near - far),
            0
        );
}
