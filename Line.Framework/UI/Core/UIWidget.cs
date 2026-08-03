using System.Numerics;
using Line.Framework.Types;

namespace Line.Framework.UI;

public abstract class UIWidget : UINode
{
    public DynamicValue<Coord2> Position { get; set; } = new(new Coord2());
    public DynamicValue<Coord2> Size { get; set; } = new(new Coord2());
    public DynamicValue<Vector2> Anchor { get; set; } = new(new Vector2(0, 0));
    public DynamicValue<bool> Visible { get; set; } = true;

    public Vector2 GetPositionOnScreen()
    {
        Vector2 si = new(0, 0);
        if (Parent != null || Parent is UIWidget i)
        {
            i = Parent as UIWidget;
            si = i.GetSizeOnScreen() * i.Anchor;
        }
        return new(
            s.Value.X * Position.Value.scale.X
                + Position.Value.offset.X
                - GetSizeOnScreen().X * Anchor.Value.X
                + p.Value.X
                - si.X,
            s.Value.Y * Position.Value.scale.Y
                + Position.Value.offset.Y
                - GetSizeOnScreen().Y * Anchor.Value.Y
                + p.Value.Y
                - si.Y
        );
    }

    public Vector2 GetSizeOnScreen()
    {
        return new(
            s.Value.X * Size.Value.scale.X + Size.Value.offset.X,
            s.Value.Y * Size.Value.scale.Y + Size.Value.offset.Y
        );
    }

    public Vector2[] GetClipArea(Vector2 source)
    {
        var p = GetPositionOnScreen();
        var s = GetSizeOnScreen();
        Vector2 ac = Anchor;
        Vector2[] vert =
        [
            new(-ac.X * s.X, -ac.Y * s.Y),
            new(-ac.X * s.X, (1 - ac.Y) * s.Y),
            new((1 - ac.X) * s.X, (1 - ac.Y) * s.Y),
            new((1 - ac.X) * s.X, -ac.Y * s.Y),
        ];

        for (int i = 0; i < vert.Length; i++)
        {
            float cos = (float)Math.Cos(Rotation * Math.PI / 180f);
            float sin = (float)Math.Sin(Rotation * Math.PI / 180f);

            var target = vert[i];
            //旋转
            var pos = target;
            target.X = pos.X * cos - pos.Y * sin;
            target.Y = pos.Y * cos + pos.X * sin;

            //缩放
            target *= Scale;

            //映射回前面
            target += ac * s;

            //到绝对
            target += p;

            //跑回NDC
            target.X = 2 * target.X / source.X - 1;
            target.Y = 1 - 2 * target.Y / source.Y;
            vert[i] = target;
        }
        return vert;
    }

    public virtual void RendererContext(RendererContextArgs args) { }

    protected UIWidget()
    {
        s = new(() =>
        {
            if (Parent is UIWidget t)
                return new(
                    t.Size.Value.offset.X + t.Size.Value.scale.X * t.s.Value.X,
                    t.Size.Value.offset.Y + t.Size.Value.scale.Y * t.s.Value.Y
                );
            return new Vector2(0);
        });
        o = new(() =>
        {
            if (Parent is UIWidget a)
                return a.o * Opacity;
            return Opacity;
        });
        p = new(() =>
        {
            if (Parent is UIWidget t)
                return new(
                    t.Position.Value.offset.X
                        + t.Position.Value.scale.X * t.s.Value.X
                        + t.p.Value.X,
                    t.Position.Value.offset.Y + t.Position.Value.scale.Y * t.s.Value.Y + t.p.Value.Y
                );
            return new Vector2(0, 0);
        });
    }

    internal float oz = 0;
    internal DynamicValue<Vector2> s { get; set; }
    public TouchModes TouchMode { get; set; } = TouchModes.All;
    internal DynamicValue<Vector2> p { get; set; }
    internal bool syncOK = false;
    internal DynamicValue<float> o { get; set; }
    internal List<Vector2[]> ClipList = [];
    public DynamicValue<float> Rotation { get; set; } = 0;
    public DynamicValue<float> Opacity { get; set; } = 1;

    public DynamicValue<Vector2> Scale { get; set; } = new Vector2(1);

    public Vector2 MousePosition(Vector2 mousePixel)
    {
        var P = GetPositionOnScreen();
        var s = GetSizeOnScreen();
        //到相对
        var tmp = mousePixel - P - Anchor * s;

        //旋转
        double r = (double)Rotation % 360d;
        r = -r;
        double cos = Math.Cos(r * Math.PI / 180f);
        double sin = Math.Sin(r * Math.PI / 180f);
        tmp = new((float)(tmp.X * cos - tmp.Y * sin), (float)(tmp.Y * cos + tmp.X * sin));

        return tmp + Anchor * s;
    }

    public virtual bool HitTest(Vector2 mousePixel)
    {
        var tmp = MousePosition(mousePixel);
        var S = GetSizeOnScreen();
        return 0 <= tmp.X && 0 <= tmp.Y && tmp.X <= S.X && tmp.Y <= S.Y;
    }

    public static UIWidget FindWidgetPointTouched(UIWidget w, Vector2 Point)
    {
        UIWidget[] Children = w.Children.OfType<UIWidget>().OrderBy(c => c.Z).ToArray();
        for (int i = Children.Length; ; )
        {
            i--;
            if (i < 0)
                break;
            if (Children[i].HitTest(Point))
            {
                switch (Children[i].TouchMode)
                {
                    case (TouchModes.All):
                        return FindWidgetPointTouched(Children[i], Point);
                    case (TouchModes.Children):
                        var t = FindWidgetPointTouched(Children[i], Point);
                        if (t != null && t != Children[i])
                            return t;
                        break;
                }
            }
        }
        return w;
    }

    public static bool IsWidgetPointTouched(UIWidget w, UIWidget t, Vector2 Point)
    {
        UIWidget[] Children = w.Children.OfType<UIWidget>().OrderBy(c => c.Z).ToArray();
        for (int i = Children.Length; ; )
        {
            i--;
            if (i < 0)
                break;
            if (Children[i].HitTest(Point))
            {
                switch (Children[i].TouchMode)
                {
                    case (TouchModes.All):
                        if (Children[i] == t)
                            return true;
                        return IsWidgetPointTouched(Children[i], t, Point);
                    case (TouchModes.Children):
                        if (Children[i] == t)
                            return false;
                        return IsWidgetPointTouched(Children[i], t, Point);
                }
            }
        }
        return false;
    }
}

public class RendererContextArgs
{
    public double X { get; set; }
    public double Y { get; set; }
    public double width { get; set; }
    public double height { get; set; }
    public UIDrawCollector Collector { get; set; }
}

public enum TouchModes
{
    None,
    Children,
    All,
}
