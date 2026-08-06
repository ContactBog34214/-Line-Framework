using System.Numerics;
using System.Text;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Veldrid;
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

layout(binding = 0) uniform sampler2D _texture;

void main()
{
    fsout_Color = texture(_texture, fsin_UV) * fsin_Color;
}";

    ShaderDescription vertexShaderDesc = new ShaderDescription(
        ShaderStages.Vertex, // 顶点着色器阶段
        Encoding.UTF8.GetBytes(VertexCode), // GLSL 源代码（转为字节数组）
        "main" // 入口函数名
    );

    ShaderDescription fragmentShaderDesc = new ShaderDescription(
        ShaderStages.Fragment,
        Encoding.UTF8.GetBytes(FragmentCode),
        "main"
    );

    // 使用局部每帧 CommandList，避免并发复用导致 Veldrid 内部状态/类型混乱
    protected GraphicsDevice gd;
    protected Shader[] _shaders;
    protected CommandList cl;
    protected Pipeline _pipeline;
    protected ResourceLayout _textureLayout;
    protected ResourceSet _textureResourceSet;
    DeviceBuffer _vertexBuffer;
    public override ResourceLayout TextureLayout => _textureLayout;

    public override void Render(Vertex[] vertices)
    {
        var screenSize = Host.Size;
        if (_shaders == null)
            CreateShader(gd);
        if (_pipeline == null)
        {
            ReCreatePipeline(Host);
        }

        var values = vertices;
        List<VertexPositionColor> vert = [];
        var tot = 0;
        List<VertexTask[]> Tasks = [];
        ResourceSet LastRs = null;
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
                    Position =
                        (idx.Position + new Vector2(1 - Host.Scale, Host.Scale - 1)) / Host.Scale,
                    Color = c,
                    UV = idx.UV,
                }
            );
        }

        if (t.Count != 0)
            Tasks.Add(t.ToArray());
        //顶点缓冲区大小检查
        uint totalSize = (uint)(tot * VertexPositionColor.SizeInBytes);
        if (_vertexBuffer == null || totalSize > _vertexBuffer.SizeInBytes)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                new BufferDescription(totalSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic)
            );
        }
        gd.UpdateBuffer(_vertexBuffer, 0, vert.ToArray());

        if (tot == 0)
            return;

        if (_pipeline == null)
            return;

        if (cl == null)
            cl = gd.ResourceFactory.CreateCommandList();
        try
        {
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, new(0, 0, 0, 0));
            cl.SetPipeline(_pipeline);

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

            cl.End();
            gd.SubmitCommands(cl);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }
    }

    public Renderer(WindowType window)
        : base(window)
    {
        gd = window.Dev;
        CreateShader(gd);
        _textureLayout = gd.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription(
                    "_texture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                )
            )
        );
        ReCreatePipeline(window);
    }

    protected Texture whiteTexture;

    protected void ReCreatePipeline(WindowType window)
    {
        _textureResourceSet?.Dispose();
        var gd = window.Dev;
        whiteTexture = gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                1,
                1,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled
            )
        );
        // 填充白色像素数据
        byte[] whitePixel = [255, 255, 255, 255];
        gd.UpdateTexture(whiteTexture, whitePixel, 0, 0, 0, 1, 1, 1, 0, 0);

        // 3. 创建资源集 (ResourceSet)
        _textureResourceSet = gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(_textureLayout, whiteTexture)
        );
        _pipeline?.Dispose();
        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription(
                "Position",
                VertexElementSemantic.TextureCoordinate,
                VertexElementFormat.Float2
            ),
            new VertexElementDescription(
                "Color",
                VertexElementSemantic.TextureCoordinate,
                VertexElementFormat.Float4
            ),
            new VertexElementDescription(
                "UV",
                VertexElementSemantic.TextureCoordinate,
                VertexElementFormat.Float2
            )
        );
        GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription
        {
            RasterizerState = new RasterizerStateDescription(
                FaceCullMode.None,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                true,
                false
            ),
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = [_textureLayout],
            ShaderSet = new ShaderSetDescription([vertexLayout], _shaders),
            Outputs = window.Dev.SwapchainFramebuffer.OutputDescription,
            BlendState = BlendStateDescription.SingleAlphaBlend,
        };

        _pipeline = window.Dev.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
    }

    protected void CreateShader(GraphicsDevice gd)
    {
        _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
    }

    public struct VertexPositionColor
    {
        public Vector2 Position; // This is the position, in normalized device coordinates.
        public RgbaFloat Color; // This is the color of the vertex.
        public Vector2 UV;

        public VertexPositionColor(Vector2 position, RgbaFloat color, Vector2 uv)
        {
            Position = position;
            Color = color;
            UV = uv;
        }

        public const uint SizeInBytes = 32;
    }

    public class VertexTask
    {
        public Vector2 Position { get; set; }
        public RgbaFloat Color { get; set; }
        public Vector2 UV { get; set; }
        public Texture Texture { get; set; }
        public ResourceSet ResourceSet { get; set; }
        public float Opacity { get; set; }
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

    private static VertexTask Export(Vertex vertex)
    {
        return new()
        {
            Position = vertex.Position,
            UV =
                vertex.UV.scale
                + vertex.UV.offset
                    / new Vector2(vertex.Texture?.Width ?? 1, vertex.Texture?.Height ?? 1),
            Color = vertex.Color,
            Texture = vertex.Texture,
            ResourceSet = vertex.ResourceSet,
            Opacity = vertex.Opacity,
        };
    }
}
