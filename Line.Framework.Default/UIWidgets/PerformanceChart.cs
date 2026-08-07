using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.Default.UIWidgets;

public class PerformanceChart : UIWidget
{
    private readonly CircularBuffer<double> Buffer = new(256);
    public DynamicValue<RgbaFloat[]> Colors { get; set; } =
        (RgbaFloat[])
            [
                new RgbaFloat(0, 127, 0, 255),
                new RgbaFloat(98, 127, 0, 255),
                new RgbaFloat(127, 0, 0, 255),
            ];
    public DynamicValue<int> Num { get; set; } = 10;

    public Func<double, short> Classifier { get; set; } =
        (arg) =>
        {
            if (arg < 5)
                return 0;
            if (arg < 20)
                return 1;
            return 2;
        };
    public double Multiple { get; set; } = 20;

    public void Update(double arg)
    {
        Buffer.Add(arg);
    }

    public int BufferSize
    {
        get => (int)Buffer.Size;
        set => Buffer.Size = value;
    }

    public override async Task RendererContext(RendererContextArgs args)
    {
        if (Num < 1)
            return;
        var collector = args.Collector;
        double markSize = EnableMark ? MarkSize : 0;

        void RenderBox(Vector2 Position, Vector2 Size, RgbaFloat color)
        {
            var tl = new Vertex(Position, color, new(new(), new(0, 0)), null, null, 1);
            var tr = new Vertex(
                Position + new Vector2(Size.X, 0),
                color,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = new Vertex(
                Position + new Vector2(0, Size.Y),
                color,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = new Vertex(
                Position + new Vector2(Size.X, Size.Y),
                color,
                new(new(), new(1, 1)),
                null,
                null,
                1
            );
            collector.DrawVertex([tl, tr, bl], this);
            collector.DrawVertex([tr, bl, br], this);
        }
        int num = Num;
        double xPtr = markSize;
        double yPtr = args.height - markSize;
        if (num > Buffer.Count)
            num = (int)Buffer.Count;
        double Width = (args.width - markSize * 2) / num;
        num = (int)Buffer.Count - num;
        RgbaFloat[] color = Colors?.Value ?? [new(255, 255, 255, 255)];
        for (int i = num; i < (int)Buffer.Count; i++)
        {
            if (i >= Buffer.Count)
                continue;
            double val = Buffer[i];
            short select = Classifier?.Invoke(val) ?? 0;
            RgbaFloat TgColor = new(255, 255, 255, 255);
            if (select < color.Length && select >= 0)
                TgColor = color[select];
            else if (color.Length != 0)
                TgColor = color[^1];
            float height = (float)(Multiple * val);
            RenderBox(new((float)xPtr, (float)yPtr - height), new((float)Width, height), TgColor);
            xPtr += Width;
        }
        if (EnableMark)
        {
            void DrawLine(double Y, RgbaFloat color) =>
                RenderBox(
                    new(0, (float)(args.height - markSize - Y)),
                    new((float)args.width, (float)markSize),
                    color
                );
            var selColor = MarkColor?.Value ?? new(1, 1, 1, 1f);
            DrawLine(0, selColor);
            double[] marks = Mark?.Value ?? [];
            if (marks.Length == 0)
                return;
            double totalHeight = marks.OrderBy(c => c).Last() * Multiple;
            var defaultCl = MarkColor?.Value ?? new(1, 1, 1, 1f);
            var dyMode = DynamicMarkColor?.Value ?? true;
            var Offset = MarkTextOffset?.Value ?? new Vector2(0);
            foreach (var i in marks)
            {
                double h = i * Multiple;
                RgbaFloat cl = defaultCl;
                if (dyMode)
                {
                    short m = Classifier?.Invoke(i) ?? -1;
                    if (m >= 0 && m < color.Length)
                        cl = color[m];
                    cl += DynamicMarkColorDelta ?? new();
                }
                DrawLine(h, cl);
                UIDrawCollector VCollector = new();
                RendererContextArgs VRender = new()
                {
                    width = args.width - 2 * markSize,
                    height = MarkFontSize,
                    Collector = VCollector,
                };
                Vector2 Sc =
                    new Vector2((float)markSize, (float)(args.height - h - MarkFontSize)) + Offset;
                Text.color = cl;
                Text.Offset = Sc;
                Text.Text = MarkPrefix?.Invoke(i) ?? i.ToString();
                await Text.RendererContext(VRender);
                foreach (var text in VCollector.Verts)
                {
                    collector.DrawVertex(text.Vert, this);
                }
            }
            DrawLine(totalHeight, selColor);

            if (totalHeight > 0)
            {
                RenderBox(
                    new(0, (float)(args.height - markSize - totalHeight)),
                    new((float)args.width, (float)markSize),
                    selColor
                );
            }
            RenderBox(
                new(0, (float)(args.height - totalHeight)),
                new((float)markSize, (float)totalHeight),
                selColor
            );
            RenderBox(
                new((float)(args.width - markSize), (float)(args.height - totalHeight)),
                new((float)markSize, (float)totalHeight),
                selColor
            );
        }
    }

    public DynamicValue<double[]> Mark { get; set; } = (double[])[5, 20];
    public DynamicValue<bool> EnableMark { get; set; } = true;
    public DynamicValue<double> MarkSize { get; set; } = 3;
    public DynamicValue<Vector2> MarkTextOffset { get; set; } = new Vector2(10, 0);
    public DynamicValue<List<string>> MarkFontId
    {
        get;
        set
        {
            field = value;
            Text.FontId = field.Value;
        }
    } = new List<string> { "" };
    public Func<double, string> MarkPrefix { get; set; } = new((db) => $"{db}");
    public DynamicValue<RgbaFloat> MarkColor { get; set; } = new RgbaFloat(1, 1, 1, 1f);
    public DynamicValue<bool> DynamicMarkColor { get; set; } = true;
    public DynamicValue<float> MarkFontSize
    {
        get => Text.FontSize;
        set => Text.FontSize = value;
    }
    public DynamicValue<RgbaFloat> DynamicMarkColorDelta { get; set; } =
        new RgbaFloat(16, 16, 16, 0);
    private readonly UIText Text;

    public PerformanceChart(ResourceManager rm)
    {
        Text = new(rm);
        Text.FontSize = 24;
    }
}
