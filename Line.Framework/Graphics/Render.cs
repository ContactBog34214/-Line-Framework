using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Line.Framework.UI;
using SharpText.Core;
using TagLib.Ape;
using Veldrid;
using Veldrid.SPIRV;
using Veldrid.Utilities;
using Vortice.Direct3D11.Debug;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.Graphics;

public class WindowsRenderer
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
    Shader[] _shaders;
    Pipeline _pipeline;
    ResourceLayout _textureLayout;
    ResourceSet _textureResourceSet;
    DeviceBuffer _vertexBuffer;
    private const uint INITIAL_BUFFER_SIZE = 1024 * 1024;
    public ResourceLayout TextureLayout => _textureLayout;

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

    public class Vertex
    {
        public Vector2 Position { get; set; }
        public RgbaFloat Color { get; set; }
        public Coord2 UV { get; set; }
        public Texture Texture { get; set; }

        public Vertex(Vector2 p, RgbaFloat c, Coord2 u, Texture rs)
        {
            Position = p;
            Color = c;
            UV = u;
            Texture = rs;
        }

        public VertexPositionColor Export()
        {
            return new(
                Position,
                Color,
                UV.scale + UV.offset / new Vector2(Texture.Width, Texture.Height)
            );
        }
    }

    class VertexTask
    {
        public Vector2 Position { get; set; }
        public RgbaFloat Color { get; set; }
        public Vector2 UV { get; set; }
        public Texture Texture { get; set; }
        public ResourceSet ResourceSet { get; set; }
    }

    Vector2 r(Vector2 a, float b)
    {
        var c = (float)Math.Cos(b);
        var s = (float)Math.Sin(b);
        return new Vector2(a.X * c + a.Y * s, a.Y * c + a.X * s);
    }

    VertexPositionColor[] GetRectVertices(VertexPositionColor[] vertex, Vector2 source, UIWidget s)
    {
        float cos = (float)Math.Cos(s.rotation * Math.PI / 180f);
        float sin = (float)Math.Sin(s.rotation * Math.PI / 180f);
        var tmp = vertex;

        for (var i = 0; i < tmp.Length; i++)
        {
            var target = tmp[i];
            //颜色处理
            RgbaFloat rgba = new(
                new(target.Color.R, target.Color.G, target.Color.B, target.Color.A * s.o)
            );
            target.Color = rgba;

            //从绝对映射到相对锚点
            var size = s.GetSizeOnScreen();
            target.Position -= s.anchor * size;

            //旋转
            var pos = target.Position;
            target.Position.X = pos.X * cos - pos.Y * sin;
            target.Position.Y = pos.Y * cos + pos.X * sin;

            //缩放
            target.Position *= s.Scale;

            //映射回前面
            target.Position += s.anchor * size;

            //到绝对
            target.Position += s.GetPositionOnScreen();

            //跑回NDC
            target.Position.X = 2 * target.Position.X / source.X - 1;
            target.Position.Y = 1 - 2 * target.Position.Y / source.Y;
            tmp[i] = target;
        }
        return tmp;
    }

    VertexPositionColor[] GetRectVertices(
        Rectangle rect,
        RgbaFloat color,
        Vector2 source,
        UIWidget s
    )
    {
        RgbaFloat f = new(color.R, color.G, color.B, color.A);
        Rectangle tmp = rect;

        //初步定位
        Vector2 tl = new(0, 0);
        Vector2 tr = new(tmp.Width, 0);
        Vector2 bl = new(0, tmp.Height);
        Vector2 br = new(tmp.Width, tmp.Height);
        RgbaFloat finalColor = f;

        //uv
        Vector2 uv_tl = new Vector2(0, 0);
        Vector2 uv_tr = new Vector2(1, 0);
        Vector2 uv_bl = new Vector2(0, 1);
        Vector2 uv_br = new Vector2(1, 1);

        VertexPositionColor[] vert =
        [
            new VertexPositionColor(tl, finalColor, uv_tl),
            new VertexPositionColor(tr, finalColor, uv_tr),
            new VertexPositionColor(bl, finalColor, uv_bl),
            new VertexPositionColor(tr, finalColor, uv_tr),
            new VertexPositionColor(br, finalColor, uv_br),
            new VertexPositionColor(bl, finalColor, uv_bl),
        ];
        return GetRectVertices(vert, source, s);
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

    public Action<BaseWindow, UIDrawCollector> UIRenderer { get; }

    public WindowsRenderer(GraphicsDevice gd)
    {
        CreateShader(gd);
        CreatePipeline(gd);
        UIRenderer = (BaseWindow window, UIDrawCollector collector) =>
        {
            if (collector == null)
            {
                collector = new();
            }
            collector.Clear();

            //刷新层级
            foreach (var item in trees(window.Root).OrderBy(c => c.Z))
            {
                if (item is UIWidget target && target.RendererContext != null)
                {
                    //同步层级
                    try
                    {
                        var t = target.parent as UIWidget;
                        if (t is UIScreen a)
                        {
                            target.oz = target.Z;
                        }
                        else
                        {
                            target.oz = target.Z + t.oz;
                        }
                    }
                    catch
                    {
                        target.oz = target.Z;
                    }
                }
            }

            //各种同步然后请求渲染内容
            foreach (var item in trees(window.Root).OrderBy(c => c.oz))
            {
                if (item is UIWidget target && target.RendererContext != null) // 添加 null 检查
                {
                    //同步渲染区大小
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
                    //同步移位
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
                    //同步透明度
                    try
                    {
                        var t = target.parent as UIWidget;
                        if (t is UIScreen a)
                        {
                            target.o = target.Opacity;
                        }
                        else
                        {
                            target.o = target.Opacity * t.o;
                        }
                    }
                    catch
                    {
                        target.o = target.Opacity;
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
            var screenSize = new Vector2(window.TargetWindow.Width, window.TargetWindow.Height);

            // 分组字典：ResourceSet -> 顶点列表
            var groups = new Dictionary<ResourceSet, List<VertexPositionColor>>();

            // 1. 处理普通矩形（默认白色纹理资源集）
            if (_textureResourceSet != null)
            {
                var defaultGroup = new List<VertexPositionColor>();
                groups[_textureResourceSet] = defaultGroup;
                foreach (var rect in collector.Rects)
                {
                    var verts = GetRectVertices(rect.Rect, rect.Color, screenSize, rect.Source);
                    defaultGroup.AddRange(verts);
                }
            }

            // 2. 处理纹理矩形（按各自的 ResourceSet 分组）
            foreach (var texCmd in collector.Textures)
            {
                if (texCmd.TextureResourceSet == null)
                    continue;
                if (!groups.TryGetValue(texCmd.TextureResourceSet, out var group))
                {
                    group = new List<VertexPositionColor>();
                    groups[texCmd.TextureResourceSet] = group;
                }
                var verts = GetRectVertices(texCmd.Rect, texCmd.Tint, screenSize, texCmd.Source);
                group.AddRange(verts);
            }

            foreach (var texCmd in collector.Textures)
            {
                if (texCmd.TextureResourceSet == null)
                    continue;
                if (!groups.TryGetValue(texCmd.TextureResourceSet, out var group))
                {
                    group = new List<VertexPositionColor>();
                    groups[texCmd.TextureResourceSet] = group;
                }
                var verts = GetRectVertices(texCmd.Rect, texCmd.Tint, screenSize, texCmd.Source);
                group.AddRange(verts);
            }

            //开始正式渲染
            // 1. 确保 Pipeline 已创建
            if (_shaders == null)
                CreateShader(gd);
            if (_pipeline == null)
            {
                CreatePipeline(gd);
            }

            // 2. 收集命令列表（已按 Z 排序）
            collector.Update();
            var commands = collector.AllCommands;
            if (commands.Count == 0)
                return;

            // 3. 第一遍遍历：计算总顶点数，并记录每个命令的起始偏移和资源集
            int totalVertexCount = 0;
            var cmdInfos =
                new List<(
                    int startVertex,
                    int vertexCount,
                    ResourceSet resourceSet,
                    VertexPositionColor[] vertices
                )>();
            foreach (var cmd in commands)
            {
                int vertexCount = 6; // 每个控件固定 6 个顶点
                ResourceSet rs = null;
                VertexPositionColor[] verts = null;

                if (cmd is UIDrawCollector.DrawRectCommand rc)
                {
                    rs = _textureResourceSet; // 默认白色纹理
                    verts = GetRectVertices(rc.Rect, rc.Color, screenSize, rc.Source);
                }
                else if (cmd is UIDrawCollector.DrawTextureCommand tc)
                {
                    if (tc.TextureResourceSet == null)
                        continue;
                    rs = tc.TextureResourceSet;
                    verts = GetRectVertices(tc.Rect, tc.Tint, screenSize, tc.Source);
                }
                else
                    continue;

                if (verts == null || verts.Length != vertexCount)
                    continue;
                cmdInfos.Add((totalVertexCount, vertexCount, rs, verts));
                totalVertexCount += vertexCount;
            }

            if (totalVertexCount == 0)
                return;

            // 4. 确保顶点缓冲区足够大
            uint totalSize = (uint)(totalVertexCount * VertexPositionColor.SizeInBytes);
            if (_vertexBuffer == null || totalSize > _vertexBuffer.SizeInBytes)
            {
                _vertexBuffer?.Dispose();
                _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                    new BufferDescription(totalSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic)
                );
            }

            // 5. 将所有控件的顶点数据写入缓冲区（不同偏移）
            foreach (var info in cmdInfos)
            {
                uint offsetBytes = (uint)(info.startVertex * VertexPositionColor.SizeInBytes);
                gd.UpdateBuffer(_vertexBuffer, offsetBytes, info.vertices);
            }

            // 6. 开始命令录制
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(_pipeline);
            cl.SetVertexBuffer(0, _vertexBuffer);

            // 7. 逐个控件绘制（每个 Draw 绑定自己的资源集）
            foreach (var info in cmdInfos)
            {
                cl.SetGraphicsResourceSet(0, info.resourceSet);
                cl.Draw((uint)info.vertexCount, 1, (uint)info.startVertex, 0);
            }

            cl.End();
            gd.SubmitCommands(cl);
        };
    }

    void DrawRectCommand(
        CommandList cl,
        GraphicsDevice gd,
        UIDrawCollector.DrawRectCommand cmd,
        Vector2 screenSize
    )
    {
        // 使用默认白色纹理资源集
        var resourceSet = _textureResourceSet; // 全局默认资源集
        var vertices = GetRectVertices(cmd.Rect, cmd.Color, screenSize, cmd.Source);
        UploadAndDraw(cl, gd, vertices, resourceSet);
    }

    void DrawTextureCommand(
        CommandList cl,
        GraphicsDevice gd,
        UIDrawCollector.DrawTextureCommand cmd,
        Vector2 screenSize
    )
    {
        // 使用纹理自带的资源集
        var resourceSet = cmd.TextureResourceSet;
        if (resourceSet == null)
            return; // 安全起见
        var vertices = GetRectVertices(cmd.Rect, cmd.Tint, screenSize, cmd.Source);
        UploadAndDraw(cl, gd, vertices, resourceSet);
    }

    // 文本命令暂不实现，留作扩展
    void DrawTextCommand(
        CommandList cl,
        GraphicsDevice gd,
        UIDrawCollector.DrawTextCommand cmd,
        Vector2 screenSize
    )
    {
        // 需要字体图集支持，暂时留空
    }

    void UploadAndDraw(
        CommandList cl,
        GraphicsDevice gd,
        VertexPositionColor[] vertices,
        ResourceSet resourceSet
    )
    {
        /*
        if (vertices.Length == 0)
            return;

        uint requiredSize = (uint)(vertices.Length * VertexPositionColor.SizeInBytes);
        if (_vertexBuffer == null || requiredSize > _vertexBuffer.SizeInBytes)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                new BufferDescription(requiredSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic)
            );
        }
        gd.UpdateBuffer(_vertexBuffer, 0, vertices);
        cl.SetVertexBuffer(0, _vertexBuffer);
        cl.SetGraphicsResourceSet(0, resourceSet);
        cl.Draw((uint)vertices.Length, 1, 0, 0);
        */
        /*
        _verts.AddRange(vertices);
        _res.Add(resourceSet);
        rs=resourceSet;
        */
    }

    List<VertexPositionColor> _verts = [];
    List<ResourceSet> _res = [];
    ResourceSet rs;

    void CreatePipeline(GraphicsDevice gd)
    {
        // 1. 创建资源布局 (ResourceLayout)
        _textureLayout = gd.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription(
                    "_texture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment
                )
            )
        );

        // 2. 创建 1x1 白色纹理
        Texture whiteTexture = gd.ResourceFactory.CreateTexture(
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
        byte[] whitePixel = new byte[] { 255, 255, 255, 255 };
        gd.UpdateTexture(whiteTexture, whitePixel, 0, 0, 0, 1, 1, 1, 0, 0);

        // 3. 创建资源集 (ResourceSet)
        _textureResourceSet = gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(_textureLayout, whiteTexture)
        );

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
            //ResourceLayouts = Array.Empty<ResourceLayout>(),
            ResourceLayouts = new[] { _textureLayout },
            ShaderSet = new ShaderSetDescription(new[] { vertexLayout }, _shaders),
            Outputs = gd.SwapchainFramebuffer.OutputDescription,
            BlendState = BlendStateDescription.SingleAlphaBlend,
        };

        _pipeline = gd.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
    }

    void CreateShader(GraphicsDevice gd)
    {
        _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
    }
}
