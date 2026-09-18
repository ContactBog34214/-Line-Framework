using System.Collections.Concurrent;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.UI;

namespace Line.Framework.Default.Graphics
{
    public class LiteCompositor : ICompositor
    {
        private protected const float Deg2Rad = MathF.PI / 180f;
        private protected readonly Dictionary<UIWidget, UIWidgetLayout> UILayoutTable = new();

        public virtual async Task<Vertex[]> Composite(UIWidget Root)
        {
            List<UIWidget> ws = trees(Root);

            UILayoutTable.Clear();

            int zIndex = 0;
            Collector collector = new();
            Task[] tasks = new Task[ws.Count];

            foreach (var i in ws)
            {
                if (i is not UIScreen _ && i.Parent is not UIWidget _)
                    continue;
                Vector2 Offset = new();
                Vector2 Size;
                float Opacity = 1;
                List<Vector2[]> clip = new();

                if (i is UIScreen sc)
                {
                    Size = sc.Size.Value.offset;
                    Opacity = 1;
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
                }
                else
                {
                    continue;
                }
                UILayoutTable.Add(i, new(Offset, Size, Opacity, zIndex, i.Rotation.Value, clip));
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
                zIndex++;
            }

            if (tasks != null) await Task.WhenAll(tasks.Where(c => c != null));

            var commands = collector.GetOrdered(ws);
            if (commands.Count == 0)
                return [];
            long TotalThreadCount = commands.Count;

            Vertex[] CTV(DrawCommand i)
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
                                    verts.Source
                                )[0];
                                tasks.Add(
                                    new(
                                        a.Position,
                                        a.Color,
                                        a.UV,
                                        c.Texture,
                                        c.ResourceSet ?? null,
                                        table.Opacity
                                    )
                                );
                            }
                        }
                        if (tasks.Count != 0)
                        {
                            List<Vertex> op = [];
                            for (int v = 0; v < tasks.Count; v += 3)
                            {
                                if (v + 2 >= tasks.Count)
                                    break;

                                op.AddRange([tasks[v + 0], tasks[v + 1], tasks[v + 2]]);
                            }
                            return [.. op];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex}");
                }
                return [];
            }

            Vertex[][] vs = new Vertex[commands.Count][];
            Parallel.For(
                0,
                TotalThreadCount,
                idx => vs[idx] = CTV(commands[(int)idx])
            );

            List<Vertex> result = [];
            foreach (var i in vs)
            {
                if (i != default)
                    result.AddRange(i);
            }

            return result.ToArray();
        }

        private protected struct UIWidgetLayout
        {
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
                Rotation = r;
            }
        }
        private protected static List<UIWidget> trees(UIWidget root)
        {
            List<UIWidget> widgets = new();
            HashSet<UIWidget> visited = new();

            void Collect(UIWidget node, int i = 0)
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
                    Collect(child as UIWidget, i + 1);
                }
            }

            Collect(root);
            return widgets;
        }

        private protected static Vector2[] GetClipArea(UIWidget tg, UIWidgetLayout table)
        {
            var p = table.Position;
            var s = table.Size;
            Vector2 ac = tg.Anchor;
            Vector2[] vert =
            [
                new(-ac.X * s.X, -ac.Y * s.Y),
                new(-ac.X * s.X, (1 - ac.Y) * s.Y),
                new((1 - ac.X) * s.X, (1 - ac.Y) * s.Y),
                new((1 - ac.X) * s.X, -ac.Y * s.Y),
            ];

            float cos = (float)Math.Cos(tg.Rotation * Math.PI / 180f);
            float sin = (float)Math.Sin(tg.Rotation * Math.PI / 180f);
            for (int i = 0; i < vert.Length; i++)
            {
                var target = vert[i];
                //旋转
                var pos = target;
                target.X = pos.X * cos - pos.Y * sin;
                target.Y = pos.Y * cos + pos.X * sin;

                //映射回前面
                target += ac * s;

                //到绝对
                target += p;

                vert[i] = target;
            }
            return vert;
        }

        private protected Vertex[] GetVertices(Vertex[] vertex, UIWidget s)
        {
            var tb = UILayoutTable[s];

            float cos = MathF.Cos(tb.Rotation * Deg2Rad);
            float sin = MathF.Sin(tb.Rotation * Deg2Rad);

            for (int i = 0; i < vertex.Length; i++)
            {
                var target = vertex[i];

                var size = tb.Size;
                target.Position -= s.Anchor * size;

                var pos = target.Position;
                target.Position = new Vector2(
                    pos.X * cos - pos.Y * sin,
                    pos.Y * cos + pos.X * sin
                );

                target.Position += s.Anchor * size;
                target.Position += tb.Position;

                vertex[i] = target;
            }

            return vertex;
        }
    }
}
