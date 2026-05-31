using System.Collections.Concurrent;
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
using Vortice.Direct3D11;
using Vortice.Direct3D11.Debug;
using Vortice.DXCore;
using Vulkan.Wayland;
using BufferDescription = Veldrid.BufferDescription;
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
        public ResourceSet ResourceSet { get; set; }
        public float Opacity { get; set; }

        public Vertex(Vector2 p, RgbaFloat c, Coord2 u, Texture t, ResourceSet rs, float o)
        {
            Position = p;
            Color = c;
            UV = u;
            Texture = t;
            ResourceSet = rs;
            Opacity = o;
        }

        public VertexTask Export()
        {
            return new()
            {
                Position = Position,
                UV = UV.scale + UV.offset / new Vector2(Texture?.Width ?? 1, Texture?.Height ?? 1),
                Color = Color,
                Texture = Texture,
                ResourceSet = ResourceSet,
                Opacity = Opacity,
            };
        }
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

    Vector2 r(Vector2 a, float b)
    {
        var c = (float)Math.Cos(b);
        var s = (float)Math.Sin(b);
        return new Vector2(a.X * c + a.Y * s, a.Y * c + a.X * s);
    }

    VertexPositionColor[] GetVertices(VertexPositionColor[] vertex, Vector2 source, UIWidget s)
    {
        float cos = (float)Math.Cos(s.rotation * Math.PI / 180f);
        float sin = (float)Math.Sin(s.rotation * Math.PI / 180f);
        var tmp = vertex;

        for (var i = 0; i < tmp.Length; i++)
        {
            var target = tmp[i];
            //颜色处理
            RgbaFloat rgba = new(
                new(target.Color.R, target.Color.G, target.Color.B, target.Color.A)
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
        return GetVertices(vert, source, s);
    }

    static List<List<UIWidget>> trees(UIWidget root)
    {
        List<List<UIWidget>> widgets = [];
        var i = 0;
        void Collect(UIWidget node, int i, float b)
        {
            if (!node.visible)
                return;
            if (widgets.Count <= i)
            {
                widgets.Add([]);
            }
            node.oz = i + b;
            widgets[i].Add(node);
            float c = 0;
            List<UIWidget> d = [];
            d.AddRange(node.children.OfType<UIWidget>());
            d.OrderBy(c => c.Z);
            foreach (var child in d)
            {
                if (!child.visible)
                    continue;
                Collect(child, i + 1, c / d.Count());
                c++;
            }
        }
        Collect(root, i, 0);
        for (int f = 0; f < widgets.Count; f++)
        {
            widgets[f].OrderBy(c => c.Z);
        }
        return widgets;
    }

    public Action<BaseWindow, UIDrawCollector> UIRenderer { get; }

    public WindowsRenderer(GraphicsDevice gd)
    {
        CreateShader(gd);
        CreatePipeline(gd);
        UIRenderer = async (BaseWindow window, UIDrawCollector collector) =>
        {
            if (collector == null)
            {
                collector = new();
            }
            collector.Clear();
            List<List<UIWidget>> widgets = trees(window.Root);
            List<UIWidget> ws = [];
            foreach (var item in widgets)
            {
                ws.AddRange(item);
            }

            //各种同步然后请求渲染内容
            foreach (var item in ws)
            {
                if (item is UIWidget target && target.RendererContext != null) // 添加 null 检查
                {
                    async void syncer()
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
                    }
                    syncer();
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

            // 3.将所有commands转为顶点们
            int totalVertexCount = 0;
            ManualResetEventSlim CTVThreadWaiter = new ManualResetEventSlim(false);
            List<Vertex[]> v = [];
            List<Action> CTVThreadPool = [];
            long CPThreadCount = 0;
            long TotalThreadCount = commands.Count;
            ConcurrentBag<(uint index, Vertex[] v)> values = new ConcurrentBag<(uint, Vertex[])>();

            void CTV(UIDrawCollector.DrawCommand i, int idx)
            {
                try
                {
                    List<Vertex> tasks = [];
                    for (int y = 0; y < 1; y++)
                    {
                        if (i.Source.GetSizeOnScreen() == new Vector2(0, 0))
                            break;
                        if (i is UIDrawCollector.DrawRectCommand r)
                        {
                            //矩形处理
                            var a = GetRectVertices(r.Rect, r.Color, screenSize, r.Source);
                            foreach (var b in a)
                            {
                                tasks.Add(
                                    new Vertex(
                                        b.Position,
                                        b.Color,
                                        new(new(), b.UV),
                                        null,
                                        _textureResourceSet,
                                        r.Source.o
                                    )
                                );
                            }
                        }
                        else if (i is UIDrawCollector.DrawTextureCommand t)
                        {
                            //带材质矩形处理
                            var a = GetRectVertices(t.Rect, t.Tint, screenSize, t.Source);
                            foreach (var b in a)
                            {
                                tasks.Add(
                                    new Vertex(
                                        b.Position,
                                        b.Color,
                                        new(new(), b.UV),
                                        t.Texture,
                                        t.TextureResourceSet,
                                        t.Source.o
                                    )
                                );
                            }
                        }
                        else if (i is UIDrawCollector.DrawVertCommand verts)
                        {
                            //顶点组处理
                            List<Vertex> v = [];
                            foreach (var c in verts.Vert)
                            {
                                var a = GetVertices(
                                    [new(c.Position, c.Color, c.Export().UV)],
                                    screenSize,
                                    verts.Source
                                )[0];
                                tasks.Add(
                                    new(
                                        a.Position,
                                        a.Color,
                                        new(new(), a.UV),
                                        c.Texture,
                                        c?.ResourceSet ?? _textureResourceSet,
                                        verts.Source.o
                                    )
                                );
                            }
                        }
                        if (tasks.Count != 0)
                        {
                            values.Add(new((uint)idx, tasks.ToArray()));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Renderer] {ex}");
                }
                finally
                {
                    CPThreadCount += 1;
                    if (CPThreadCount >= TotalThreadCount)
                    {
                        CTVThreadWaiter.Set();
                    }
                }
            }

            Parallel.For(
                0,
                TotalThreadCount,
                idx =>
                {
                    var i = commands[(int)idx];
                    CTV(i, (int)idx);
                }
            );
            //CTVThreadWaiter.Wait();

            // 4. 第一遍遍历：转换一下，顺手上传
            List<VertexPositionColor> vert = [];
            List<VertexTask[]> Tasks = [];
            ResourceSet LastRs = null;
            List<VertexTask> t = [];
            foreach (var i1 in values.OrderBy(c => c.index).ToList())
            {
                var i = i1.v;
                foreach (var a in i)
                {
                    var idx = a.Export();
                    totalVertexCount += 1;

                    /*
                     *如果资源集与上次不一样就上传重置
                     *管它的，反正优化了
                     */
                    if (LastRs != a.ResourceSet)
                    {
                        if (t.Count != 0)
                            Tasks.Add(t.ToArray());
                        t.Clear();
                        LastRs = a.ResourceSet;
                    }

                    t.Add(idx);
                    var c = idx.Color;
                    c = new(c.R, c.G, c.B, c.A * idx.Opacity);
                    vert.Add(
                        new()
                        {
                            Position = idx.Position,
                            Color = c,
                            UV = idx.UV,
                        }
                    );
                }
            }
            if (t.Count != 0)
                Tasks.Add(t.ToArray());
            //顶点缓冲区大小检查
            uint totalSize = (uint)(totalVertexCount * VertexPositionColor.SizeInBytes);
            if (_vertexBuffer == null || totalSize > _vertexBuffer.SizeInBytes)
            {
                _vertexBuffer?.Dispose();
                _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                    new BufferDescription(totalSize, BufferUsage.VertexBuffer | BufferUsage.Dynamic)
                );
            }
            gd.UpdateBuffer(_vertexBuffer, 0, vert.ToArray());

            if (totalVertexCount == 0)
                return;

            // 6. 开始命令录制
            cl.Begin();
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.SetPipeline(_pipeline);
            cl.SetVertexBuffer(0, _vertexBuffer);

            // 7. 逐个控件绘制（每个 Draw 绑定自己的资源集）
            uint index = 0;
            foreach (var i in Tasks)
            {
                uint num = (uint)i.Length;
                cl.SetGraphicsResourceSet(0, i[0].ResourceSet);
                cl.Draw(num, 1, index, 0);
                index += num;
            }

            cl.End();
            gd.SubmitCommands(cl);
        };
    }

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
