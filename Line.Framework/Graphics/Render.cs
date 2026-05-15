using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.Graphics;

public static class WindowsRenderer
{
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

    static ShaderDescription vertexShaderDesc = new ShaderDescription(
        ShaderStages.Vertex, // 顶点着色器阶段
        Encoding.UTF8.GetBytes(VertexCode), // GLSL 源代码（转为字节数组）
        "main" // 入口函数名
    );

    static ShaderDescription fragmentShaderDesc = new ShaderDescription(
        ShaderStages.Fragment,
        Encoding.UTF8.GetBytes(FragmentCode),
        "main"
    );
    static Shader[] _shaders;
    static Pipeline _pipeline;
    static DeviceBuffer _vertexBuffer;
    static DeviceBuffer _indexBuffer;

    public struct VertexPositionColor
    {
        public Vector2 Position; // This is the position, in normalized device coordinates.
        public RgbaFloat Color; // This is the color of the vertex.

        public VertexPositionColor(Vector2 position, RgbaFloat color)
        {
            Position = position;
            Color = color;
        }

        public const uint SizeInBytes = 24;
    }

    public static Action<BaseWindow> UIRenderer { get; } =
        (BaseWindow window) =>
        {
            var cl = window.commandList;
            var tmp = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector2(-0.75f, -0.75f), RgbaFloat.Red),
                new VertexPositionColor(new Vector2(0f, 0.75f), RgbaFloat.Green),
                new VertexPositionColor(new Vector2(0.75f, -0.75f), RgbaFloat.Blue),
            };
            ushort[] a = [0, 1, 2];
            if (_vertexBuffer == null)
            {
                _vertexBuffer = window.Dev.ResourceFactory.CreateBuffer(
                    new BufferDescription(
                        VertexPositionColor.SizeInBytes * 4,
                        BufferUsage.VertexBuffer
                    )
                );
                window.Dev.UpdateBuffer(_vertexBuffer, 0, tmp);
            }
            if (_indexBuffer == null)
            {
                _indexBuffer = window.Dev.ResourceFactory.CreateBuffer(
                    new BufferDescription(sizeof(ushort) * 4, BufferUsage.IndexBuffer)
                );
                window.Dev.UpdateBuffer(_indexBuffer, 0, a);
            }
            var gd = window.Dev;
            gd.UpdateBuffer(_vertexBuffer, 0, tmp);
            gd.UpdateBuffer(_indexBuffer, 0, a);
            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                new VertexElementDescription(
                    "Position",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float2
                ),
                new VertexElementDescription(
                    "Color",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float4
                )
            );
            if (_shaders == null)
            {
                _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            }
            if (_pipeline == null)
            {
                GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
                pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
                pipelineDescription.RasterizerState = new RasterizerStateDescription(
                    cullMode: FaceCullMode.Back,
                    fillMode: PolygonFillMode.Solid,
                    frontFace: FrontFace.Clockwise,
                    depthClipEnabled: true,
                    scissorTestEnabled: false
                );
                pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
                pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
                pipelineDescription.ShaderSet = new ShaderSetDescription(
                    vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
                    shaders: _shaders
                );
                pipelineDescription.Outputs = gd.SwapchainFramebuffer.OutputDescription;
                _pipeline = gd.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
            }
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            cl.SetVertexBuffer(0, _vertexBuffer);
            cl.SetPipeline(_pipeline);
            cl.DrawIndexed(
                indexCount: 3,
                instanceCount: 1,
                indexStart: 0,
                vertexOffset: 0,
                instanceStart: 0
            );
            cl.End();
            gd.SubmitCommands(cl);
        };
}
