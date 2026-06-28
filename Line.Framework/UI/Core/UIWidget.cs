using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace Line.Framework.UI;

public abstract class UIWidget : UINode
{
    public Coord2 Position { get; set; } = new();
    public Coord2 Size { get; set; } = new();
    public Vector2 anchor { get; set; } = new(0, 0);
    public bool visible { get; set; } = true;

    public Vector2 GetPositionOnScreen()
    {
        Vector2 si = new(0, 0);
        if (parent != null || parent is UIWidget i)
        {
            i = parent as UIWidget;
            si = i.GetSizeOnScreen() * i.anchor;
        }
        return new(
            s.X * Position.scale.X
                + Position.offset.X
                - GetSizeOnScreen().X * anchor.X
                + p.X
                - si.X,
            s.Y * Position.scale.Y + Position.offset.Y - GetSizeOnScreen().Y * anchor.Y + p.Y - si.Y
        );
    }

    public Vector2 GetSizeOnScreen()
    {
        return new(s.X * Size.scale.X + Size.offset.X, s.Y * Size.scale.Y + Size.offset.Y);
    }

    public virtual void RendererContext(RendererContextArgs args){}
    public float oz = 0;
    public float rotation { get; set; } = 0;
    public float Opacity { get; set; } = 1;
    public Vector2 s { get; set; } = new(0, 0);
    public Vector2 p { get; set; } = new(0, 0);
    public float o { get; set; } = 1;
    public Vector2 Scale { get; set; } = new(1, 1);

    public Vector2 MousePosition(Vector2 mousePixel)
    {
        var S = GetSizeOnScreen();
        var P = GetPositionOnScreen();
        //到相对
        var tmp = mousePixel - P - anchor * S;

        //旋转
        double r = (double)rotation % 360d;
        r = 180d - r;
        double cos = Math.Cos(r * Math.PI / 180f);
        double sin = Math.Sin(r * Math.PI / 180f);
        tmp = new((float)(tmp.X * cos - tmp.Y * sin), (float)(tmp.Y * cos + tmp.X * sin));

        tmp += anchor * S;
        return tmp;
    }

    public virtual bool HitTest(Vector2 mousePixel)
    {
        var tmp = MousePosition(mousePixel);
        var S = GetSizeOnScreen();
        var P = GetPositionOnScreen();
        return 0 <= tmp.X && 0 <= tmp.Y && tmp.X <= S.X && tmp.Y <= S.Y;
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
