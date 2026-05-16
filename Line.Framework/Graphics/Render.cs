using System.Numerics;
using System.Text;
using Line.Framework.UI;
using TagLib.Ape;
using Veldrid;
using Veldrid.SPIRV;
using Veldrid.Utilities;
using Vortice.Direct3D11.Debug;
using Rectangle = System.Drawing.RectangleF;

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
    private const uint INITIAL_BUFFER_SIZE = 1024 * 1024;

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

    static Vector2 r(Vector2 a, float b)
    {
        var c = (float)Math.Cos(b);
        var s = (float)Math.Sin(b);
        return new Vector2(a.X * c + a.Y * s, a.Y * c + a.X * s);
    }

    static VertexPositionColor[] GetRectVertices(
        Rectangle rect,
        RgbaFloat color,
        float rotation,
        Vector2 anchor,
        float Opacity,
        Vector2 source,
        UIWidget s
    )
    {
        float cos = (float)Math.Cos(rotation * Math.PI / 180f);
        float sin = (float)Math.Sin(rotation * Math.PI / 180f);
        RgbaFloat f = new(color.R, color.G, color.B, color.A * Opacity);
        var scale=s.Size.scale;
        Rectangle tmp = new()
        {
            X = rect.X * 2 / source.X - 1,
            Y = 1 - rect.Y * 2 / source.Y,
            Width = rect.Width * 2 / source.X,
            Height = rect.Height * 2 / source.Y,
        };

        //初步定位
        Vector2 tl = new(anchor.X * -tmp.Width, anchor.Y * tmp.Height);
        Vector2 tr = new((1 - anchor.X) * tmp.Width, anchor.Y * tmp.Height);
        Vector2 bl = new(anchor.X * -tmp.Width, (1 - anchor.Y) * -tmp.Height);
        Vector2 br = new((1 - anchor.X) * tmp.Width, (1 - anchor.Y) * -tmp.Height);

        //旋转
        tl = new(tl.X * cos - tl.Y * sin, tl.Y * cos + tl.X * sin);
        tr = new(tr.X * cos - tr.Y * sin, tr.Y * cos + tr.X * sin);
        bl = new(bl.X * cos - bl.Y * sin, bl.Y * cos + bl.X * sin);
        br = new(br.X * cos - br.Y * sin, br.Y * cos + br.X * sin);

        //映射
        var pos = new Vector2(tmp.X, tmp.Y);
        tl = tl + pos;
        tr = tr + pos;
        bl = bl + pos;
        br = br + pos;
        RgbaFloat finalColor = f;

        // 返回两个三角形共 6 个顶点
        return
        [
            new VertexPositionColor(tl, finalColor), // 三角形1
            new VertexPositionColor(tr, finalColor),
            new VertexPositionColor(bl, finalColor),
            new VertexPositionColor(tr, finalColor), // 三角形2
            new VertexPositionColor(br, finalColor),
            new VertexPositionColor(bl, finalColor),
        ];
    }

    static List<UIWidget> trees(UIWidget root)
    {
        var result = new List<UIWidget>();
        void Collect(UIWidget node)
        {
            if (!node.visible)
                return;
            result.Add(node);
            foreach (var child in node.children.OfType<UIWidget>())
            {
                if (!child.visible)
                    continue;
                Collect(child);
            }
        }
        Collect(root);
        return result;
    }

    public static Action<BaseWindow, UIDrawCollector> UIRenderer { get; } =
        (BaseWindow window, UIDrawCollector collector) =>
        {
            if (collector == null)
            {
                collector = new();
            }
            collector.Clear();
            //检查矩形
            foreach (var item in trees(window.Root).OrderBy(c=>c.z))
            {
                if (item is UIWidget target && target.RendererContext != null) // 添加 null 检查
                {
                    try
                    {
                        var t = target.parent as UIWidget;
                        target.s = new(
                            t.Size.offset.X + t.Size.scale.X * t.s.X,
                            t.Size.offset.Y + t.Size.scale.Y * t.s.Y
                        );
                    }
                    catch
                    {
                        target.s = new(window.TargetWindow.Width, window.TargetWindow.Height);
                    }
                    try
                    {
                        var t = target.parent as UIWidget;
                        if (t is UIScreen a)
                        {
                            target.p = new(0, 0);
                        }
                        else
                        {
                            target.p = new(
                                t.Position.offset.X + t.Position.scale.X * t.s.X + t.p.X,
                                t.Position.offset.Y + t.Position.scale.Y * t.s.Y + t.p.Y
                            );
                        }
                    }
                    catch
                    {
                        target.s = new(0, 0);
                    }
                    var source = target.s;
                    target.RendererContext(
                        new RendererContextArgs
                        {
                            X =
                                target.Position.offset.X
                                + target.Position.scale.X * source.X
                                + target.p.X,
                            Y =
                                target.Position.offset.Y
                                + target.Position.scale.Y * source.Y
                                + target.p.Y,
                            width = target.Size.offset.X + target.Size.scale.X * source.X,
                            height = target.Size.offset.Y + target.Size.scale.Y * source.Y,
                            Collector = collector,
                        }
                    );
                }
            }
            var gd = window.Dev;
            var cl = window.commandList;

            // 1. 收集所有矩形的顶点
            List<VertexPositionColor> allVertices = new List<VertexPositionColor>();
            foreach (var rect in collector.Rects)
            {
                var verts = GetRectVertices(
                    rect.Rect,
                    rect.Color,
                    rect.Rotation,
                    rect.Anchor,
                    rect.Opacity,
                    new(window.TargetWindow.Width, window.TargetWindow.Height),
                    rect.Source
                );
                allVertices.AddRange(verts); // 每个矩形4个顶点
            }

            // 2. 创建/更新顶点缓冲区
            uint requiredSize = (uint)(allVertices.Count * VertexPositionColor.SizeInBytes);
            DeviceBuffer vertexBuffer;
            if (_vertexBuffer == null || requiredSize > _vertexBuffer.SizeInBytes)
            {
                _vertexBuffer?.Dispose();
                vertexBuffer = gd.ResourceFactory.CreateBuffer(
                    new BufferDescription(
                        requiredSize,
                        BufferUsage.VertexBuffer | BufferUsage.Dynamic
                    )
                );
                _vertexBuffer = vertexBuffer;
            }
            else
            {
                vertexBuffer = _vertexBuffer;
            }
            gd.UpdateBuffer(vertexBuffer, 0, allVertices.ToArray());

            // 3. 确保 Pipeline 已创建
            if (_shaders == null)
                _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            if (_pipeline == null)
            {
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
                GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription
                {
                    RasterizerState = new RasterizerStateDescription(
                        FaceCullMode.Back,
                        PolygonFillMode.Solid,
                        FrontFace.Clockwise,
                        true,
                        false
                    ),
                    PrimitiveTopology = PrimitiveTopology.TriangleList,
                    ResourceLayouts = Array.Empty<ResourceLayout>(),
                    ShaderSet = new ShaderSetDescription(new[] { vertexLayout }, _shaders),
                    Outputs = gd.SwapchainFramebuffer.OutputDescription,
                    BlendState = BlendStateDescription.SingleAlphaBlend,
                };

                _pipeline = gd.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
            }

            // 4. 记录命令
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.SetPipeline(_pipeline);

            // 一次绘制所有矩形（每个矩形4个顶点）
            cl.Draw((uint)allVertices.Count, 1, 0, 0);

            cl.End();
            gd.SubmitCommands(cl);
        };
}
