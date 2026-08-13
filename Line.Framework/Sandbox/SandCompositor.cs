using System.Collections.Concurrent;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.Sandbox
{
    public class SandCompositor : ICompositor
    {
        private protected readonly Dictionary<UIWidget, UIWidgetLayout> UILayoutTable = new();
        public async Task<Vertex[]> Composite(UIWidget root)
        {
            bool clipMode = EnableClip;
            List<UIWidget> ws = trees(root);

            UILayoutTable.Clear();

            int zIndex = 0;
            Vector2 ScreenSize = new();
            Collector collector = new();
            Task[] tasks = new Task[ws.Count - 1];

            foreach (var i in ws)
            {
                try
                {
                    if (!(i is UIScreen _) && !(i.Parent is UIWidget _))
                        continue;
                    Vector2 Offset = new();
                    Vector2 Size;
                    float Opacity = 1;
                    List<Vector2[]> clip = new();

                    if (i == root)
                    {
                        Size = ((UIScreen)i).Size.Value.offset;
                        Opacity = 1;
                        UILayoutTable.Add(i, new(Offset, Size, Opacity, zIndex, i.Rotation.Value, clip));
                        continue;
                    }
                    else if (i.Parent is UIWidget parentWidget)
                    {
                        if (!UILayoutTable.TryGetValue(parentWidget, out var u))
                            continue;
                        var p = i.Position.Value + parentWidget.ChildrenOffset.Value;
                        var s = i.Size.Value;
                        var o = i.Opacity;
                        Offset = p.offset + p.scale * u.Size + u.Position;
                        Size = s.offset + s.scale * u.Size;
                        Offset -= i.Anchor * Size;
                        Opacity = Math.Max(o * u.Opacity, 0);
                        Opacity = Math.Min(Opacity, 1);
                        if (clipMode)
                        {
                            if (u.ClipList != null)
                                clip.AddRange(u.ClipList);
                            clip.Add(GetClipArea(i, Size));
                        }
                        UILayoutTable.Add(i, new(Offset, Size, Opacity, zIndex, i.Rotation.Value, clip));
                    }
                    else
                    {
                        continue;
                    }
                    tasks[zIndex] = Task.Run(async () =>
                    {
                        var item = i;
                        if (item is UIWidget target && UILayoutTable.TryGetValue(item, out var table))
                        {
                            try
                            {
                                await target.RendererContext(
                                    new RendererContextArgs
                                    {
                                        X = table.Position.X,
                                        Y = table.Position.Y,
                                        width = table.Size.X,
                                        height = table.Size.Y,
                                        Collector = collector,
                                    }
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"{ex}");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
                zIndex++;
            }

            await Task.WhenAll(tasks.Where(c => c != null));
            if (UILayoutTable.TryGetValue(root, out var val))
                ScreenSize = val.Size;

            var commands = collector.GetOrdered(ws);
            if (commands.Count == 0)
                return [];
            long TotalThreadCount = commands.Count;
            var values = new ConcurrentBag<(uint, Vertex[])>();

            void CTV(DrawCommand i, int idx)
            {
                try
                {
                    List<Vertex> tasks = [];
                    for (int y = 0; y < 1; y++)
                    {
                        if (
                            !(UILayoutTable.TryGetValue(i.Source, out var table))
                            || table.Size == new Vector2(0, 0)
                        )
                            break;
                        {
                            var verts = i;
                            //顶点组处理
                            List<Vertex> v = [];
                            foreach (var c in verts.Vert)
                            {
                                var a = GetVertices(
                                    [
                                        new(
                                            c.Position,
                                            c.Color,
                                            c.UV,
                                            c.Texture,
                                            c.ResourceSet,
                                            c.Opacity
                                        ),
                                    ],
                                    ScreenSize,
                                    verts.Source
                                )[0];
                                tasks.Add(
                                    new(
                                        a.Position,
                                        a.Color,
                                        a.UV,
                                        c.Texture,
                                        c?.ResourceSet ?? null,
                                        table.Opacity
                                    )
                                    {
                                        Clips = table.ClipList,
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
                                Vertex[] VertToVPC(Vertex[] vertices)
                                {
                                    var ctmp = new List<Vertex> { };

                                    for (int vr = 0; vr < vertices.Length; vr++)
                                    {
                                        var tg = vertices[vr];
                                        ctmp.Add(tg);
                                    }
                                    return ctmp.ToArray();
                                }

                                tmp.Add([tasks[v + 0], tasks[v + 1], tasks[v + 2]]);
                                var st = tasks[v];

                                for (int clip = 0; clip < st.Clips.Count; clip++)
                                {
                                    var p = st.Clips[clip];
                                    Vertex[] quad =
                                    [
                                        new(p[0], new(1, 1, 1, 1f), new(), null, null, 1),
                                        new(p[1], new(1, 1, 1, 1f), new(), null, null, 1),
                                        new(p[2], new(1, 1, 1, 1f), new(), null, null, 1),
                                        new(p[3], new(1, 1, 1, 1f), new(), null, null, 1),
                                    ];
                                    List<Vertex[]> tmp2 = [];
                                    if (EnableClip)
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
                                                            item2.UV,
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
            }

            await Parallel.ForAsync(
                0,
                TotalThreadCount,
                async (idx, _) => CTV(commands[(int)idx], (int)idx)
            );

            List<Vertex> result = [];
            foreach (var i in values.OrderBy(c => -c.Item1).Select(c => c.Item2))
            {
                result.AddRange(i);
            }
            return result.ToArray();
        }

        public DynamicValue<bool> EnableClip { get; set; } = true;

        private protected struct UIWidgetLayout
        {
            public readonly List<Vector2[]> ClipList { get; }
            public Vector2 Size { get; set; }
            public Vector2 Position { get; set; }
            public float Opacity { get; set; }
            public float Z { get; set; }
            public float Rotation { get; set; }

            public UIWidgetLayout(
                Vector2 p,
                Vector2 s,
                float o,
                float z,
                float r,
                List<Vector2[]> Clip
            )
            {
                Position = p;
                Size = s;
                Opacity = o;
                Z = z;
                ClipList = Clip;
                Rotation = r;
            }
        }

        private protected static List<UIWidget> trees(UIWidget root)
        {
            List<UIWidget> widgets = new();
            HashSet<UIWidget> visited = new();

            void Collect(UIWidget node)
            {
                if (node == null)
                    return;

                if (!visited.Add(node))
                    return; // 已访问或正在访问

                if (!node.Visible)
                    return;

                widgets.Add(node);

                var sortedChildren = node.Children.Where(c => c != null).OrderBy(c => c.Index);

                foreach (var child in sortedChildren)
                {
                    Collect(child as UIWidget);
                }
            }

            Collect(root);
            return widgets;
        }

        private protected static Vector2[] GetClipArea(UIWidget tg, Vector2 renderArea)
        {
            var p = tg.GetPositionOnScreen();
            var s = tg.GetSizeOnScreen();
            Vector2 ac = tg.Anchor;
            Vector2[] vert =
            [
                new(-ac.X * s.X, -ac.Y * s.Y),
                new(-ac.X * s.X, (1 - ac.Y) * s.Y),
                new((1 - ac.X) * s.X, (1 - ac.Y) * s.Y),
                new((1 - ac.X) * s.X, -ac.Y * s.Y),
            ];

            for (int i = 0; i < vert.Length; i++)
            {
                float cos = (float)Math.Cos(tg.Rotation * Math.PI / 180f);
                float sin = (float)Math.Sin(tg.Rotation * Math.PI / 180f);

                var target = vert[i];
                //旋转
                var pos = target;
                target.X = pos.X * cos - pos.Y * sin;
                target.Y = pos.Y * cos + pos.X * sin;

                //映射回前面
                target += ac * s;

                //到绝对
                target += p;

                // 不在此处映射到 NDC，保留为屏幕像素坐标以便在渲染阶段统一转换
                vert[i] = target;
            }
            return vert;
        }

        private protected Vertex[] GetVertices(Vertex[] vertex, Vector2 source, UIWidget s)
        {
            if (!UILayoutTable.TryGetValue(s, out var tb))
                return vertex;
            float cos = (float)Math.Cos(tb.Rotation * Math.PI / 180f);
            float sin = (float)Math.Sin(tb.Rotation * Math.PI / 180f);
            var tmp = vertex;

            for (var i = 0; i < tmp.Length; i++)
            {
                var target = tmp[i];
                //颜色处理
                RgbaFloat rgba = new(
                    target.Color.R,
                    target.Color.G,
                    target.Color.B,
                    target.Color.A
                );
                target.Color = rgba;

                //从绝对映射到相对锚点
                var size = tb.Size;
                target.Position -= s.Anchor * size;

                //旋转
                var pos = target.Position;
                var rp = target.Position;
                rp.X = pos.X * cos - pos.Y * sin;
                rp.Y = pos.Y * cos + pos.X * sin;

                //映射回前面
                rp += s.Anchor * size;

                //到绝对
                rp += tb.Position;

                target.Position = rp;
                tmp[i] = target;
            }
            return tmp;
        }
    }

    public static class GeometryClipper
    {
        // ===================== PUBLIC =====================

        public static Vertex[][] ClipTriangleByQuad(Vertex[] triangle, Vertex[] quad)
        {
            if (triangle == null || triangle.Length != 3)
                throw new ArgumentException("triangle must be 3 vertices");
            if (quad == null || quad.Length != 4)
                throw new ArgumentException("quad must be 4 vertices");

            var poly = new List<Vertex>(3);
            for (int i = 0; i < 3; i++)
                poly.Add(ToClip(triangle[i]));

            var clipPoly = new List<Vector2>(4);
            for (int i = 0; i < 4; i++)
                clipPoly.Add(quad[i].Position);

            var clipped = ClipByConvexPolygon(poly, clipPoly);

            if (clipped.Count < 3)
                return Array.Empty<Vertex[]>();

            var tris = TriangulateFan(clipped);

            var result = new Vertex[tris.Count / 3][];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vertex[3];
                for (int j = 0; j < 3; j++)
                    result[i][j] = tris[i * 3 + j];
            }

            return result;
        }

        // ===================== CLIP =====================

        private static List<Vertex> ClipByConvexPolygon(
            List<Vertex> polygon,
            List<Vector2> clipPolygon
        )
        {
            if (polygon.Count < 3 || clipPolygon.Count < 3)
                return new List<Vertex>();

            var clip = EnsureCCW(clipPolygon);
            var output = new List<Vertex>(polygon);

            for (int i = 0; i < clip.Count; i++)
            {
                Vector2 a = clip[i];
                Vector2 b = clip[(i + 1) % clip.Count];

                output = ClipByEdge(output, a, b);

                if (output.Count < 3)
                    return new List<Vertex>();
            }

            return Clean(output);
        }

        private static List<Vertex> ClipByEdge(List<Vertex> poly, Vector2 a, Vector2 b)
        {
            var output = new List<Vertex>();
            if (poly.Count == 0)
                return output;

            for (int i = 0; i < poly.Count; i++)
            {
                var cur = poly[i];
                var next = poly[(i + 1) % poly.Count];

                bool curIn = Inside(cur.Position, a, b);
                bool nextIn = Inside(next.Position, a, b);

                if (curIn && nextIn)
                {
                    output.Add(cur);
                    output.Add(next);
                }
                else if (curIn && !nextIn)
                {
                    output.Add(cur);
                    output.Add(Intersect(cur, next, a, b));
                }
                else if (!curIn && nextIn)
                {
                    output.Add(Intersect(cur, next, a, b));
                    output.Add(next);
                }
            }

            return output;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b)
        {
            return Cross(b - a, p - a) >= -1e-5f;
        }

        private static Vertex Intersect(Vertex p1, Vertex p2, Vector2 a, Vector2 b)
        {
            Vector2 r = p2.Position - p1.Position;
            Vector2 s = b - a;

            float rxs = Cross(r, s);

            if (Math.Abs(rxs) < 1e-6f)
            {
                return p1;
            }

            float t = Cross(a - p1.Position, s) / rxs;
            t = Math.Clamp(t, 0f, 1f);

            Vector2 pos = p1.Position + r * t;

            RgbaFloat col = new RgbaFloat(
                p1.Color.R + (p2.Color.R - p1.Color.R) * t,
                p1.Color.G + (p2.Color.G - p1.Color.G) * t,
                p1.Color.B + (p2.Color.B - p1.Color.B) * t,
                p1.Color.A + (p2.Color.A * p2.Opacity - p1.Color.A * p1.Opacity) * t
            );

            Vector2 uv =
                p1.UV.scale
                + p1.UV.offset / new Vector2(p1.Texture?.Width ?? 1, p1.Texture?.Height ?? 1)
                + (
                    (
                        p2.UV.scale
                        + p2.UV.offset
                            / new Vector2(p2.Texture?.Width ?? 1, p2.Texture?.Height ?? 1)
                    )
                    - (
                        p1.UV.scale
                        + p1.UV.offset
                            / new Vector2(p1.Texture?.Width ?? 1, p1.Texture?.Height ?? 1)
                    )
                ) * t;

            return new Vertex(pos, col, new(new(), uv), p1.Texture, p1.ResourceSet, 1);
        }

        // ===================== TRIANGULATE (SAFE FAN) =====================

        private static List<Vertex> TriangulateFan(List<Vertex> poly)
        {
            var result = new List<Vertex>();
            if (poly.Count < 3)
                return result;

            var p = Clean(poly);
            if (p.Count < 3)
                return result;

            var baseV = p[0];

            for (int i = 1; i < p.Count - 1; i++)
            {
                result.Add(baseV);
                result.Add(p[i]);
                result.Add(p[i + 1]);
            }

            return result;
        }

        // ===================== CLEAN =====================

        private static List<Vertex> Clean(List<Vertex> input)
        {
            var outList = new List<Vertex>();

            for (int i = 0; i < input.Count; i++)
            {
                var cur = input[i];
                var prev = input[(i - 1 + input.Count) % input.Count];

                if ((cur.Position - prev.Position).LengthSquared() < 1e-10f)
                    continue;

                outList.Add(cur);
            }

            if (outList.Count < 3)
                return outList;

            var final = new List<Vertex>();

            int n = outList.Count;

            for (int i = 0; i < n; i++)
            {
                var a = outList[(i - 1 + n) % n].Position;
                var b = outList[i].Position;
                var c = outList[(i + 1) % n].Position;

                if (Math.Abs(Cross(b - a, c - a)) < 1e-10f)
                    continue;

                final.Add(outList[i]);
            }

            return final.Count >= 3 ? final : outList;
        }

        // ===================== UTIL =====================

        private static List<Vector2> EnsureCCW(List<Vector2> v)
        {
            float area = 0;

            for (int i = 0; i < v.Count; i++)
            {
                var a = v[i];
                var b = v[(i + 1) % v.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            if (area < 0)
            {
                var r = new List<Vector2>(v);
                r.Reverse();
                return r;
            }

            return [.. v];
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private static Vertex ToClip(Vertex v) => v;
    }

    public class Collector : UIDrawCollector
    {
        private readonly ConcurrentDictionary<UIWidget, List<DrawCommand>> Vertex = [];
        private bool VertsDirty = true;
        public override List<DrawCommand> Verts
        {
            get
            {
                if (VertsDirty)
                {
                    VertsDirty = false;
                    field.Clear();
                    foreach (var i in Vertex.Values)
                        field.AddRange(i);
                }
                return field;
            }
        } = [];

        public override void DrawVertex(Vertex[] v, UIWidget source)
        {
            if (v.Length % 3 != 0)
            {
                var t = v.ToList();
                bool two = v.Length % 3 == 2;
                t.RemoveAt(t.Count - 1);
                if (two)
                    t.RemoveAt(t.Count - 1);
                v = t.ToArray();
            }
            DrawCommand tmp = new()
            {
                Vert = v,
                Z = 0,
                Source = source,
            };
            if (Vertex.TryGetValue(source, out var val))
            {
                val.Add(tmp);
            }
            else
                Vertex.TryAdd(source, [tmp]);
            VertsDirty = true;
        }

        public List<DrawCommand> GetOrdered(List<UIWidget> ws)
        {
            List<DrawCommand> result = [];
            foreach (var i in ws)
            {
                if (Vertex.TryGetValue(i, out var val))
                    result.AddRange(val);
            }
            return result;
        }
    }
}
