using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Line.Framework.Default.Graphics.Veldrid;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Types;
using Veldrid.SPIRV;

namespace Line.Framework.Default.Graphics;

public class Renderer : RendererType
{
    public const string VertexCode =
        @"
#version 450
layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;
layout(location = 2) in vec2 UV;

layout(location = 0) out vec4 fsin_Color;
layout(location = 1) out vec2 fsin_UV;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
    fsin_UV = UV;
}";

    public const string FragmentCode =
        @"
#version 450
layout(location = 0) in vec4 fsin_Color;
layout(location = 1) in vec2 fsin_UV;

layout(location = 0) out vec4 fsout_Color;

// set = 0 是抽象层的统一约定（与 Veldrid 的 ResourceBindingModel.Default 一致）。
// SDL3 后端会在编译 GLSL 时自动改写成它自己的编号
//（片元采样器在 set 2），因此这里绝不要手写 set = 2 ——
// 那样 SDL3 能跑、Veldrid 会因找不到对应 set 而采样到 0，表现为黑屏。
layout(set = 0, binding = 0) uniform sampler2D _texture;

void main()
{
    fsout_Color = texture(_texture, fsin_UV) * fsin_Color;
    //fsout_Color = fsin_Color;
}";

    ShaderCreateInfo vertexShaderDesc = new()
    {
        Stage = ShaderStage.Vertex,
        Bytes = Encoding.UTF8.GetBytes(VertexCode),
        EntryPoint = "main", // 入口函数名
    };

    ShaderCreateInfo fragmentShaderDesc = new()
    {
        Stage = ShaderStage.Fragment,
        Bytes = Encoding.UTF8.GetBytes(FragmentCode),
        EntryPoint = "main",
    };

    // 使用局部每帧 CommandList，避免并发复用导致 Veldrid 内部状态/类型混乱
    protected IGraphicsDevice gd;
    protected IShader[] _shaders;
    protected ICommandList cl;
    protected IPipeline _pipeline;
    protected IResourceLayout _textureLayout;
    protected IResourceSet _textureResourceSet;
    IDeviceBuffer _vertexBuffer;
    public override IResourceLayout TextureLayout => _textureLayout;

    public override void Render(IEnumerable<Vertex> vertices, IRenderTarget renderTarget)
    {
        if (!Host.Exists)
            return;
        var screenSize = Host.Size;
        if (_shaders == null)
            CreateShader(gd);
        if (_pipeline == null)
        {
            ReCreatePipeline(Host, renderTarget);
        }

        var values = vertices;
        List<VertexPositionColor> vert = [];
        var tot = 0;
        List<VertexTask[]> Tasks = [];
        IResourceSet LastRs = null;
        List<VertexTask> t = [];

        foreach (var i in values)
        {
            var idx = Export(i);
            tot += 1;

            /*
             *如果资源集与上次不一样就上传重置
             *管它的，反正优化了
             */
            if (LastRs != idx.ResourceSet)
            {
                if (t.Count != 0)
                    Tasks.Add(t.ToArray());
                t.Clear();
                LastRs = idx.ResourceSet;
            }
            // 从屏幕像素坐标转换到 NDC：x -> [-1,1], y -> [-1,1] (Veldrid/OpenGL 风格)
            if (screenSize.X <= 0 || screenSize.Y <= 0)
            {
                Log.Error($"Invalid screen size: {screenSize}");
                continue;
            }
            idx.Position = new(
                2f * idx.Position.X / screenSize.X - 1f,
                1f - 2f * idx.Position.Y / screenSize.Y
            );

            if (idx.Texture == null)
            {
                idx.Texture = whiteTexture;
            }
            if (idx.ResourceSet == null)
            {
                idx.ResourceSet = _textureResourceSet;
            }

            t.Add(idx);
            var c = idx.Color;
            c = new(c.R, c.G, c.B, c.A * idx.Opacity);
            vert.Add(
                new()
                {
                    // 缩放必须在这里做，不能交给视口 —— 原因见 Render 里
                    // 「为什么这里不用 SetViewport 做缩放」那段注释：
                    // 这行除法同时把 ndc 从 [-1, 2s-1] 压回 [-1, 1]，
                    // 而视口改变不了裁剪范围。
                    Position =
                        (idx.Position + new Vector2(1 - Host.Scale, Host.Scale - 1)) / Host.Scale,
                    Color = c,
                    UV = idx.UV,
                }
            );
        }

        if (t.Count != 0)
            Tasks.Add([.. t]);
        //顶点缓冲区大小检查
        uint totalSize = (uint)(tot * Unsafe.SizeOf<VertexPositionColor>());
        if (totalSize <= 0)
            return;
        if (_vertexBuffer == null || totalSize > _vertexBuffer.SizeInBytes)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                new BufferCreateInfo()
                {
                    SizeInBytes = totalSize,
                    Usage = BufferUsage.Vertex | BufferUsage.Dynamic,
                }
            );
        }
        _vertexBuffer.Update(0, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(vert)));

        if (tot == 0)
            return;

        if (_pipeline == null)
            return;

        cl ??= gd.ResourceFactory.CreateCommandList();
        try
        {
            using (cl.Record())
            {
                cl.SetRenderTarget(renderTarget);
                cl.ClearColorTarget(0, new(0, 0, 0, 0));
                cl.SetPipeline(_pipeline);

                // ---- 为什么这里不用 SetViewport 做缩放 ----
                //
                // 曾经试图把 UI 缩放从逐顶点变换改为视口：
                //     旧: ndc' = (ndc + (1 - s, s - 1)) / s
                //     新: 视口 (0, 0, W/s, H/s) + 顶点直传
                //
                // 数学上两者确实逐像素等价 —— 但只在 ndc ∈ [-1, 1] 时成立，
                // 而这个前提在这里<b>不成立</b>：
                //   UIScreen 的尺寸是 (W*s, H*s)，而下面换算 NDC 用的
                //   screenSize 是 Host.Size（逻辑尺寸 W, H），所以
                //   ndc 的实际范围是 [-1, 2s-1]（s=2 时是 [-1, 3]）。
                //
                // 那一行除法做了两件事：缩放 1/s，以及把 [-1, 2s-1]
                // 压回 [-1, 1]。视口改变不了裁剪范围 —— 超出 [-1,1] 的顶点
                // 在 clip 阶段就被裁掉了，等不到视口映射。实测 s=2 时
                // 只剩左上 1/4 区域有画面，且三角形被裁成直角三角。
                //
                // 要用视口实现缩放，前提是先让 ndc 落在 [-1,1] 内，
                // 也就是让 screenSize 与 UIScreen 的尺寸语义一致 ——
                // 那属于 UI 坐标系重构，不在这一步的范围内。

                cl.SetVertexBuffer(0, _vertexBuffer);

                // 逐个控件绘制（每个 Draw 绑定自己的资源集）
                uint index = 0;
                foreach (var i in Tasks)
                {
                    uint num = (uint)i.Length;
                    var rs = i[0].ResourceSet ?? _textureResourceSet;
                    cl.SetGraphicsResourceSet(0, rs);
                    cl.Draw(num, 1, index, 0);
                    index += num;
                }
            }
            gd.SubmitCommands(cl);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }
    }

    public Renderer(WindowType window, SwapchainCreateInfo createInfo)
        : base(window)
    {
        gd = window.Dev;
        _textureLayout = gd.ResourceFactory.CreateResourceLayout(
            new()
            {
                Elements =
                [
                    new() { Kind = ResourceKind.TextureSampler, Stages = ShaderStage.Fragment },
                ],
            }
        );
        CreateShader(gd);
        //ReCreatePipeline(window);
    }

    protected ITexture whiteTexture;

    protected void ReCreatePipeline(WindowType window, IRenderTarget renderTarget)
    {
        _textureResourceSet?.Dispose();
        var gd = window.Dev;
        whiteTexture = gd.ResourceFactory.CreateTexture(
            new()
            {
                Width = 1,
                Height = 1,
                ArrayLayers = 1,
                MipLevels = 1,
                Format = PixelFormat.R8G8B8A8UNorm,
                Usage = TextureUsage.Sampled,
            }
        );
        // 填充白色像素数据
        byte[] whitePixel = [255, 255, 255, 255];
        whiteTexture.Update(whitePixel, 0, 0, 0, 1, 1, 1, 0, 0);

        // 3. 创建资源集 (ResourceSet)
        _textureResourceSet = gd.ResourceFactory.CreateResourceSet(
            new() { Layout = _textureLayout, Texture = whiteTexture }
        );
        _pipeline?.Dispose();
        VertexLayoutDescription vertexLayout = new()
        {
            Attributes =
            [
                new() { Format = VertexFormat.Float2, Offset = 0 },
                new() { Format = VertexFormat.Float4, Offset = 8 },
                new() { Format = VertexFormat.Float2, Offset = 24 },
            ],
            Stride = 32,
        };
        GraphicsDescription pipelineDescription = new()
        {
            RasterizerState = new()
            {
                CullMode = FaceCullMode.None,
                FillMode = PolygonFillMode.Solid,
                FrontFace = FrontFace.Clockwise,
                DepthClipEnabled = false,
                ScissorTestEnabled = true,
            },
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ShaderSet = new() { VertexLayouts = [vertexLayout], Shaders = _shaders },
            Outputs = renderTarget.OutputFormat,
            BlendState = BlendStateDescription.SingleAlphaBlend,
        };

        _pipeline = window.Dev.ResourceFactory.CreatePipeline(
            new() { Mode = pipelineDescription, ResourceLayouts = [_textureLayout] }
        );
    }

    protected void CreateShader(IGraphicsDevice gd)
    {
        fragmentShaderDesc.ResourceLayout = _textureLayout;
        _shaders =
        [
            .. ((ShaderCreateInfo[])[vertexShaderDesc, fragmentShaderDesc]).Select(
                gd.ResourceFactory.CreateShader
            ),
        ];
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    protected struct VertexPositionColor
    {
        [FieldOffset(0)]
        public Vector2 Position; // This is the position, in normalized device coordinates.

        [FieldOffset(8)]
        public RgbaFloat Color; // This is the color of the vertex.

        [FieldOffset(24)]
        public Vector2 UV;

        public VertexPositionColor(Vector2 position, RgbaFloat color, Vector2 uv)
        {
            Position = position;
            Color = color;
            UV = uv;
        }

        public const uint SizeInBytes = 32;
    }

    protected struct VertexTask : IRecyclable
    {
        public Vector2 Position { get; set; }
        public RgbaFloat Color { get; set; }
        public Vector2 UV { get; set; }
        public ITexture Texture { get; set; }
        public IResourceSet ResourceSet { get; set; }
        public float Opacity { get; set; }

        public static VertexTask New(
            Vector2 p,
            RgbaFloat c,
            Vector2 u,
            ITexture t,
            IResourceSet rs,
            float o
        )
        {
            var result = new VertexTask();
            result.Position = p;
            result.Color = c;
            result.UV = u;
            result.Texture = t;
            result.ResourceSet = rs;
            result.Opacity = o;
            return result;
        }

        public void Reset()
        {
            Position = default;
            Color = default;
            UV = default;
            Texture = default;
            ResourceSet = default;
            Opacity = default;
        }
    }

    public override void Dispose()
    {
        cl?.Dispose();
        _textureLayout?.Dispose();
        _pipeline?.Dispose();
        if (_shaders != null)
            foreach (var i in _shaders)
                i?.Dispose();
        _vertexBuffer?.Dispose();
        _textureResourceSet?.Dispose();
    }

    protected static VertexTask Export(Vertex vertex)
    {
        return new()
        {
            Position = vertex.Position,
            Color = vertex.Color,
            UV =
                vertex.UV.scale
                + vertex.UV.offset
                    / new Vector2(vertex.Texture?.Width ?? 1, vertex.Texture?.Height ?? 1),
            Texture = vertex.Texture,
            ResourceSet = vertex.ResourceSet,
            Opacity = vertex.Opacity,
        };
    }
}
