using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using Line.Framework.IO;
using Line.Framework.Types;
using Line.Framework.UI;
using Veldrid;
using Veldrid.SPIRV;
using static Line.Framework.Graphics.WindowsRenderer;
using BufferDescription = Veldrid.BufferDescription;
using Rectangle = System.Drawing.RectangleF;
using RgbaFloat = Veldrid.RgbaFloat;

namespace Line.Framework.Graphics;

public enum GraphicBackend
{
    Metal,
    Direct3D,
    Vulkan,
    OpenGL,
}

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

    public class VertexTask
    {
        public Vector2 Position { get; set; }
        public RgbaFloat Color { get; set; }
        public Vector2 UV { get; set; }
        public Texture Texture { get; set; }
        public ResourceSet ResourceSet { get; set; }
        public float Opacity { get; set; }
    }

    static VertexPositionColor[] GetVertices(
        VertexPositionColor[] vertex,
        Vector2 source,
        UIWidget s
    )
    {
        float cos = (float)Math.Cos(s.Rotation * Math.PI / 180f);
        float sin = (float)Math.Sin(s.Rotation * Math.PI / 180f);
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
            target.Position -= s.Anchor * size;

            //旋转
            var pos = target.Position;
            target.Position.X = pos.X * cos - pos.Y * sin;
            target.Position.Y = pos.Y * cos + pos.X * sin;

            //缩放
            target.Position *= s.Scale;

            //映射回前面
            target.Position += s.Anchor * size;

            //到绝对
            target.Position += s.GetPositionOnScreen();

            //跑回NDC
            target.Position.X = 2 * target.Position.X / source.X - 1;
            target.Position.Y = 1 - 2 * target.Position.Y / source.Y;
            tmp[i] = target;
        }
        return tmp;
    }

    static Dictionary<UINode, treeCache> TreeCache = [];

    record treeCache(nint version, List<UIWidget> Tree);

    static List<UIWidget> trees(UIWidget root)
    {
        if (TreeCache.TryGetValue(root, out treeCache cache))
            if (cache.version == root.NodeTreeVersion)
                return cache.Tree;
        List<UIWidget> widgets = new();
        HashSet<UIWidget> visited = new();

        int i = 0;

        void Collect(UIWidget node)
        {
            if (node == null)
                return;

            if (!visited.Add(node))
                return; // 已访问或正在访问

            if (!node.Visible)
                return;

            node.oz = i++;
            widgets.Add(node);

            var sortedChildren = node.Children.Where(c => c != null).OrderBy(c => c.Index);

            foreach (var child in sortedChildren)
            {
                Collect(child as UIWidget);
            }
        }

        Collect(root);
        try
        {
            TreeCache.Remove(root);
        }
        catch (Exception)
        {
            Log.Debug($"Root not found");
        }
        TreeCache.Add(root, new(root.NodeTreeVersion, widgets));
        return widgets;
    }

    internal uint BufferIndex = 0;

    public WindowsRenderer(GraphicsDevice gd)
    {
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
    }

    public virtual void UIRenderer(Window window, UIDrawCollector collector)
    {
        var gd = window.Dev;
        var cl = window.commandList;
        var screenSize = window.Size;
        if (collector == null)
        {
            collector = new();
        }
        collector.Clear();
        List<UIWidget> ws = [];
        ws.AddRange(trees(window.Root));

        //各种同步然后请求渲染内容
        for (int it = 0; it < ws.Count; it++)
        {
            var item = ws[it];
            if (item is UIWidget target && target.RendererContext != null)
            {
                HashSet<UIWidget> visited = new();
                void syncer(UIWidget target)
                {
                    if (!visited.Add(target))
                        return;
                    var t = target.Parent as UIWidget;
                    if (t != null && !t.syncOK)
                        syncer(t);
                    //同步剪切链
                    try
                    {
                        target.ClipList.Clear();
                        if (t != null)
                            target.ClipList.AddRange(t.ClipList);
                        target.ClipList.Add(target.GetClipArea(screenSize));
                    }
                    catch
                    {
                        target.ClipList.Clear();
                    }
                    visited.Remove(target);
                }
                syncer(target);
            }
        }
        Parallel.For(
            0,
            ws.Count,
            i =>
            {
                var item = ws[i];
                if (item is UIWidget target && target.RendererContext != null)
                {
                    var source = target.s;
                    try
                    {
                        target.RendererContext(
                            new RendererContextArgs
                            {
                                X =
                                    target.Position.Value.offset.X
                                    + target.Position.Value.scale.X * source.Value.X
                                    + target.p.Value.X,
                                Y =
                                    target.Position.Value.offset.Y
                                    + target.Position.Value.scale.Y * source.Value.Y
                                    + target.p.Value.Y,
                                width =
                                    target.Size.Value.offset.X
                                    + target.Size.Value.scale.X * source.Value.X,
                                height =
                                    target.Size.Value.offset.Y
                                    + target.Size.Value.scale.Y * source.Value.Y,
                                Collector = collector,
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"{ex}");
                    }
                }
            }
        );

        //开始正式渲染
        // 1. 确保 Pipeline 已创建
        if (_shaders == null)
            CreateShader(gd);
        if (_pipeline == null)
        {
            CreatePipeline(window);
        }

        // 2. 收集命令列表（已按 Z 排序）
        collector.Update();
        var commands = collector.AllCommands;
        if (commands.Count == 0)
            return;

        // 3.将所有commands转为顶点们
        int totalVertexCount = 0;
        ManualResetEventSlim CTVThreadWaiter = new ManualResetEventSlim(false);
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
                    {
                        var verts = i;
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
                                {
                                    Clips = i.Source.ClipList,
                                }
                            );
                        }
                    }
                    if (tasks.Count != 0)
                    {
                        for (int v = 0; v < tasks.Count; v += 3)
                        {
                            if (v + 2 >= tasks.Count)
                                break;
                            List<Vertex[]> tmp = [];
                            VertexPositionColor[] VertToVPC(Vertex[] vertices)
                            {
                                var ctmp = new List<VertexPositionColor> { };

                                for (int vr = 0; vr < vertices.Length; vr++)
                                {
                                    var tg = vertices[vr];
                                    ctmp.Add(
                                        new()
                                        {
                                            Position = tg.Position,
                                            UV = tg.Export().UV,
                                            Color = tg.Color,
                                        }
                                    );
                                }
                                return ctmp.ToArray();
                            }

                            tmp.Add([tasks[v + 0], tasks[v + 1], tasks[v + 2]]);
                            var st = tasks[v];
                            for (int clip = 0; clip < st.Clips.Count; clip++)
                            {
                                var p = st.Clips[clip];
                                VertexPositionColor[] quad =
                                [
                                    new(p[0], RgbaFloat.White, new(0, 0)),
                                    new(p[1], RgbaFloat.White, new(0, 0)),
                                    new(p[2], RgbaFloat.White, new(0, 0)),
                                    new(p[3], RgbaFloat.White, new(0, 0)),
                                ];
                                List<Vertex[]> tmp2 = [];
                                for (int ptr = 0; ptr < tmp.Count; ptr++)
                                {
                                    foreach (
                                        var item in GeometryClipper.ClipTriangleByQuad(
                                            VertToVPC(tmp[ptr]),
                                            quad
                                        )
                                    )
                                    {
                                        List<Vertex> vertices = [];
                                        foreach (var item2 in item)
                                        {
                                            vertices.Add(
                                                new(
                                                    item2.Position,
                                                    item2.Color,
                                                    new(new(), item2.UV),
                                                    st.Texture,
                                                    st.ResourceSet,
                                                    st.Opacity
                                                )
                                            );
                                        }
                                        tmp2.Add(vertices.ToArray());
                                    }
                                }
                                tmp.Clear();
                                tmp.AddRange(tmp2);
                            }
                            foreach (var item in tmp)
                            {
                                values.Add(new((uint)idx, item));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
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

        if (window != null && window.ParallelRender)
            Parallel.For(
                0,
                TotalThreadCount,
                idx =>
                {
                    if (window == null)
                        return;
                    var i = commands[(int)idx];
                    CTV(i, (int)idx);
                }
            );
        else
            for (int idx = 0; idx < TotalThreadCount; idx++)
                CTV(commands[idx], idx);

        // 4. 第一遍遍历：转换一下，顺手上传
        List<VertexPositionColor> vert = [];
        List<VertexTask[]> Tasks = [];
        ResourceSet LastRs = null;
        List<VertexTask> t = [];

        foreach (var i in values.OrderBy(a => a.index).Select(i1 => i1.v))
        {
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
                        Position =
                            (idx.Position + new Vector2(1 - window.Scale, window.Scale - 1))
                            / window.Scale,
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

        if (_pipeline == null)
            return;
        try
        {
            cl.Begin();
        }
        catch (Exception ex)
        {
            Log.Error($" {ex}");
        }
        cl.SetFramebuffer(window.Dev.SwapchainFramebuffer);

        cl.ClearColorTarget(0, new(0, 0, 0, 0));
        cl.SetPipeline(_pipeline);
        cl.SetVertexBuffer(0, _vertexBuffer);

        // 7. 逐个控件绘制（每个 Draw 绑定自己的资源集）
        uint index = 0;
        foreach (var i in Tasks)
        {
            uint num = (uint)i.Length;
            cl.SetGraphicsResourceSet(0, i[0].ResourceSet ?? _textureResourceSet);
            cl.Draw(num, 1, index, 0);
            index += num;
        }

        cl.End();
        gd.SubmitCommands(cl);
    }

    internal void ReCreatePipeline(Window window)
    {
        _textureResourceSet?.Dispose();
        var gd = window.Dev;
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

    void CreatePipeline(Window window)
    {
        ReCreatePipeline(window);
    }

    void CreateShader(GraphicsDevice gd)
    {
        _shaders = gd.ResourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
    }
}

public static class VertexExtensions
{
    public static VertexTask Export(this Vertex vertex)
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
