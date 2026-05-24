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
    static ResourceLayout _textureLayout;
    static ResourceSet _textureResourceSet;
    static DeviceBuffer _vertexBuffer;
    private const uint INITIAL_BUFFER_SIZE = 1024 * 1024;
    public static ResourceLayout TextureLayout => _textureLayout;

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
        Vector2 source,
        UIWidget s
    )
    {
        float cos = (float)Math.Cos(rotation * Math.PI / 180f);
        float sin = (float)Math.Sin(rotation * Math.PI / 180f);
        RgbaFloat f = new(color.R, color.G, color.B, color.A * s.o);
        var scale = s.Size.scale;
        Rectangle tmp = new()
        {
            X = (s.GetPositionOnScreen().X+rect.X) * 2 / source.X - 1,
            Y = 1 - (s.GetPositionOnScreen().Y+rect.Y) * 2 / source.Y,
            Width = rect.Width * 2 / source.X,
            Height = rect.Height * 2 / source.Y,
        };

        //初步定位
        Vector2 tl = new(0,tmp.Height);
        Vector2 tr = new(tmp.Width,tmp.Height);
        Vector2 bl = new(0,0);
        Vector2 br = new(tmp.Width,0);
        tl.Y-=tmp.Height;
        tr.Y-=tmp.Height;
        bl.Y-=tmp.Height;
        br.Y-=tmp.Height;

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

        //uv
        Vector2 uv_tl = new Vector2(0, 0);
        Vector2 uv_tr = new Vector2(1, 0);
        Vector2 uv_bl = new Vector2(0, 1);
        Vector2 uv_br = new Vector2(1, 1);

        // 返回两个三角形共 6 个顶点
        return
        [
            new VertexPositionColor(tl, finalColor, uv_tl),
            new VertexPositionColor(tr, finalColor, uv_tr),
            new VertexPositionColor(bl, finalColor, uv_bl),
            new VertexPositionColor(tr, finalColor, uv_tr),
            new VertexPositionColor(br, finalColor, uv_br),
            new VertexPositionColor(bl, finalColor, uv_bl),
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
                    var verts = GetRectVertices(
                        rect.Rect,
                        rect.Color,
                        rect.Rotation,
                        rect.Anchor,
                        screenSize,
                        rect.Source
                    );
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
                var verts = GetRectVertices(
                    texCmd.Rect,
                    texCmd.Tint,
                    texCmd.Rotation,
                    texCmd.Anchor,
                    screenSize,
                    texCmd.Source
                );
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
                var verts = GetRectVertices(
                    texCmd.Rect,
                    texCmd.Tint,
                    texCmd.Rotation,
                    texCmd.Anchor,
                    screenSize,
                    texCmd.Source
                );
                group.AddRange(verts);
            }

            // 3. 确保 Pipeline 已创建
            if (_shaders == null)
                _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            if (_pipeline == null)
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

            collector.Update();

            // 4. 记录命令
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(_pipeline);

            _verts.Clear();
            _res.Clear();

            foreach (var cmd in collector.AllCommands)
            {
                // 根据命令类型生成顶点并绘制
                switch (cmd)
                {
                    case UIDrawCollector.DrawRectCommand rectCmd:
                        DrawRectCommand(cl, gd, rectCmd, screenSize);
                        break;
                    case UIDrawCollector.DrawTextureCommand texCmd:
                        DrawTextureCommand(cl, gd, texCmd, screenSize);
                        break;
                    case UIDrawCollector.DrawTextCommand textCmd:
                        DrawTextCommand(cl, gd, textCmd, screenSize);
                        break;
                }
            }
            /*
            //动态调整顶点缓冲区
            uint requiredSize = (uint)_verts.Count * VertexPositionColor.SizeInBytes;
            if (_vertexBuffer == null || requiredSize > _vertexBuffer.SizeInBytes)
            {
                _vertexBuffer?.Dispose();
                _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                    new BufferDescription(
                        requiredSize,
                        BufferUsage.VertexBuffer | BufferUsage.Dynamic
                    )
                );
            }
            gd.UpdateBuffer(_vertexBuffer,0,_verts.ToArray());
            cl.SetVertexBuffer(0,_vertexBuffer);
            cl.SetGraphicsResourceSet(0,rs);
            cl.Draw((uint)_verts.ToArray().Length, 1, 0, 0);
            */

            cl.End();
            gd.SubmitCommands(cl);
        };

    static void DrawRectCommand(
        CommandList cl,
        GraphicsDevice gd,
        UIDrawCollector.DrawRectCommand cmd,
        Vector2 screenSize
    )
    {
        // 使用默认白色纹理资源集
        var resourceSet = _textureResourceSet; // 全局默认资源集
        var vertices = GetRectVertices(
            cmd.Rect,
            cmd.Color,
            cmd.Rotation,
            cmd.Anchor,
            screenSize,
            cmd.Source
        );
        UploadAndDraw(cl, gd, vertices, resourceSet);
    }

    static void DrawTextureCommand(
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
        var vertices = GetRectVertices(
            cmd.Rect,
            cmd.Tint,
            cmd.Rotation,
            cmd.Anchor,
            screenSize,
            cmd.Source
        );
        UploadAndDraw(cl, gd, vertices, resourceSet);
    }

    // 文本命令暂不实现，留作扩展
    static void DrawTextCommand(
        CommandList cl,
        GraphicsDevice gd,
        UIDrawCollector.DrawTextCommand cmd,
        Vector2 screenSize
    )
    {
        // 需要字体图集支持，暂时留空
    }

    static void UploadAndDraw(
        CommandList cl,
        GraphicsDevice gd,
        VertexPositionColor[] vertices,
        ResourceSet resourceSet
    )
    {
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

        /*
        _verts.AddRange(vertices);
        _res.Add(resourceSet);
        rs=resourceSet;
        */
    }

    static List<VertexPositionColor> _verts = [];
    static List<ResourceSet> _res = [];
    static ResourceSet rs;
}
