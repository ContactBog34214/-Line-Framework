using System.Numerics;
using System.Text;
using Line.Framework.Graphics;
using Veldrid;
using Veldrid.OpenGLBinding;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;
using Vulkan.Xlib;

namespace Line.Framework.UI.DefaultWidget;

public class UIBox : UIWidget
{
    CommandList _commandList;
    BaseWindow _window;
    public Vector3 color = new(255, 255, 255);
    DeviceBuffer _vertexBuffer;
    DeviceBuffer _indexBuffer;
    Pipeline _pipeline;
    Shader[] _shaders;

    public UIBox(BaseWindow w)
    {
        _window = w;
        if (_commandList == null)
        {
            _commandList = _window.Dev.ResourceFactory.CreateCommandList();
        }
        this.RendererContext = args =>
        {
            var a = 0f;
            var b = 0f;
            var c = 0f;
            var d = 0f;
            a = (float)(-anchor.X * args.width);
            b = (float)((1 - anchor.X) * args.width);
            c = (float)(-anchor.Y * args.height);
            d = (float)((1 - anchor.Y) * args.height);
            var tl = r(new Vector2(a, c), rotation) + Position;
            var tr = r(new Vector2(b, c), rotation) + Position;
            var bl = r(new Vector2(a, d), rotation) + Position;
            var br = r(new Vector2(b, d), rotation) + Position;
            var cl = new RgbaFloat(color.X, color.Y, color.Z, Opacity);
            var tmp = new WindowsRenderer.VertexPositionColor[]
            {
                new(tl, cl),
                new(tr, cl),
                new(bl, cl),
                new(br, cl),
            };
            UInt16[] index = [0, 1, 2, 3];
            if (_vertexBuffer == null)
            {
                _vertexBuffer = _window.Dev.ResourceFactory.CreateBuffer(
                    new BufferDescription(
                        WindowsRenderer.VertexPositionColor.SizeInBytes * 4,
                        BufferUsage.VertexBuffer
                    )
                );
            }
            if (_indexBuffer == null)
            {
                _indexBuffer = _window.Dev.ResourceFactory.CreateBuffer(
                    new BufferDescription(sizeof(UInt16) * 4, BufferUsage.IndexBuffer)
                );
            }
            w.Dev.UpdateBuffer(_vertexBuffer, 0, tmp);
            w.Dev.UpdateBuffer(_indexBuffer, 0, index);
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
                )
            );
            if (_shaders == null)
            {
                ShaderDescription vertexShaderDesc = new ShaderDescription(
                    ShaderStages.Vertex,
                    Encoding.UTF8.GetBytes(WindowsRenderer.VertexCode),
                    "main"
                );
                ShaderDescription fragmentShaderDesc = new ShaderDescription(
                    ShaderStages.Fragment,
                    Encoding.UTF8.GetBytes(WindowsRenderer.FragmentCode),
                    "main"
                );

                _shaders = _window.Dev.ResourceFactory.CreateFromSpirv(
                    vertexShaderDesc,
                    fragmentShaderDesc
                );
            }
            if (_pipeline == null)
            {
                GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
                pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
                ;
                pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
                    depthTestEnabled: true,
                    depthWriteEnabled: true,
                    comparisonKind: ComparisonKind.LessEqual
                );
                pipelineDescription.RasterizerState = new RasterizerStateDescription(
                    cullMode: FaceCullMode.Back,
                    fillMode: PolygonFillMode.Solid,
                    frontFace: FrontFace.Clockwise,
                    depthClipEnabled: true,
                    scissorTestEnabled: false
                );
                pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
                pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
                pipelineDescription.Outputs = _window.Dev.SwapchainFramebuffer.OutputDescription;
                _pipeline = _window.Dev.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
            }
        };
    }

    Vector2 r(Vector2 a, float b)
    {
        var c = (float)Math.Cos(rotation);
        var s = (float)Math.Sin(rotation);
        return new Vector2(a.X * c + a.Y * s, a.Y * c + a.X * s);
    }
}
