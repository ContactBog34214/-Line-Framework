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
                    // 缩放不再在这里做：交由 SetViewport 在光栅化阶段完成。
                    // 见 Render 里设置视口处的推导说明。
                    Position = idx.Position,
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

                // ---- UI 缩放交给视口 --------------------------------------
                // 原来每个顶点在 CPU 端做：
                //     ndc' = (ndc + (1 - s, s - 1)) / s
                // 展开后可以证明它精确等价于「把整个绘制压到
                // 左上角起、尺寸为 (W/s, H/s) 的视口」：
                //
                //   x: pixel = (ndc' + 1)/2 * W
                //            = ((ndc + 1 - s)/s + 1)/2 * W
                //            = (ndc + 1)/2 * (W/s)
                //   y: pixel = (1 - ndc')/2 * H
                //            = (1 - ndc)/2 * (H/s)
                //
                // 也就是说视口取 (0, 0, W/s, H/s) 时两者逐像素相同 ——
                // 锚点都是左上角，不引入任何取整或采样差异。
                //
                // 结合 UIScreen 的设定可以确认语义没变：根控件的逻辑尺寸是
                // (W*s, H*s)（见 UIScreen.UpdateScreenSize），它的右边界
                // x = W*s 在新方案下落到像素 W/s*s = W，正好是整屏右缘；
                // 旧方案靠 ndc' = 1 落到同一处。两边行为一致。
                //
                // 好处不只是省掉每帧每个顶点的一次加法 + 一次除法：
                // 顶点保持真实 NDC，纹理采样发生在最终分辨率上，
                // 而原先是先把几何缩小再画，等于用低分辨率采样。
                //
                // 必须在 SetRenderTarget 之后：换目标会重建渲染通道，
                // 视口是通道状态。尺寸用渲染目标的像素尺寸，不是逻辑尺寸。
                float uiScale = Host.Scale;
                if (uiScale > 0 && renderTarget.Width > 0 && renderTarget.Height > 0)
                {
                    // 即使 uiScale == 1 也显式设置：两个后端对「换目标后视口
                    // 是否回到默认值」的行为不保证一致，显式设一次让结果确定。
                    cl.SetViewport(
                        0,
                        0,
                        renderTarget.Width / uiScale,
                        renderTarget.Height / uiScale,
                        0f,
                        1f
                    );
                }

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
