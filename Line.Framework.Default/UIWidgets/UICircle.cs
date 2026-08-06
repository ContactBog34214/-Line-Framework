using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.Default.UIWidgets;

public class UICircle : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 1f);
    public DynamicValue<uint> Precision { get; set; } = 20;
    public Vector2 Middle { get; set; } = new(0.5f);
    private Vertex[] verticesCache = [];
    private float lastP = 0;
    private long Hash = 0;

    public override async Task RendererContext(RendererContextArgs args)
    {
        var cl = args.Collector;
        if (Precision < 3)
            return;
        var HashCode = (long)color.GetHashCode() + Middle.GetHashCode() + Size.GetHashCode();
        if (Hash == HashCode && lastP == Precision.Value)
        {
            cl.DrawVertex(verticesCache, this);
            return;
        }
        Hash = HashCode;
        lastP = Precision;
        List<Vertex> v = [];
        var middle = new Vertex(
            new((float)args.width * Middle.X, (float)args.height * Middle.Y),
            color,
            new(new(), new()),
            null,
            null,
            1f
        );
        var sz = new Vector2((float)args.width, (float)args.height);
        for (int i = 0; i < Precision; i++)
        {
            var rt = (float)i / Precision * 360f;
            float cos = (float)Math.Cos(rt * Math.PI / 180f);
            float sin = (float)Math.Sin(rt * Math.PI / 180f);
            var v1p = new Vector2(cos + 1f, sin + 1f);
            v1p /= 2f;
            v1p *= sz;
            var v1 = new Vertex(v1p, color, new(new(), new()), null, null, 1f);
            rt = (float)(i + 1) / Precision * 360f;
            cos = (float)Math.Cos(rt * Math.PI / 180f);
            sin = (float)Math.Sin(rt * Math.PI / 180f);
            var v2p = new Vector2(cos + 1f, sin + 1f);
            v2p /= 2f;
            v2p *= sz;
            var v2 = new Vertex(v2p, color, new(new(), new()), null, null, 1f);
            cl.DrawVertex([middle, v1, v2], this);
            v.AddRange([middle, v1, v2]);
        }
        verticesCache = v.ToArray();
    }

    public override bool HitTest(Vector2 mousePixel)
    {
        var local = MousePosition(mousePixel); // 相对于控件左上角
        var size = GetSizeOnScreen();
        float halfW = size.X / 2f;
        float halfH = size.Y / 2f;
        float dx = local.X - halfW;
        float dy = local.Y - halfH;
        // 直接使用椭圆不等式
        return (dx * dx) / (halfW * halfW) + (dy * dy) / (halfH * halfH) <= 1f;
    }
}
