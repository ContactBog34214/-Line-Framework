using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace Line.Framework.UI;

public class UIWidget : UINode
{
    public Coord2 Position { get; set; } = new();
    public Coord2 Size { get; set; } = new();
    public Vector2 anchor { get; set; } = new(0, 0);
    public bool visible { get; set; } = true;

    public Vector2 GetPositionOnScreen()
    {
        return new(
            s.X * Position.scale.X + Position.offset.X - GetSizeOnScreen().X * anchor.X,
            s.Y * Position.scale.Y + Position.offset.Y - GetSizeOnScreen().Y * anchor.Y
        );
    }

    public Vector2 GetSizeOnScreen()
    {
        return new(s.X * Size.scale.X + Size.offset.X, s.Y * Size.scale.Y + Size.offset.Y);
    }

    public Action<RendererContextArgs> RendererContext;
    public float z { get; set; } = 0;
    public float rotation { get; set; } = 0;
    public float Opacity { get; set; } = 1;
    public Vector2 s { get; set; } = new(0, 0);
    public Vector2 p { get; set; } = new(0, 0);
    public float o { get; set; } = 1;

    public static bool HitTest(Vector2 position, Vector2 Size, Vector2 mousePixel)
    {
        var tmp = mousePixel - position;
        return 0 <= tmp.X && 0 <= tmp.Y && tmp.X <= Size.X && tmp.Y <= Size.Y;
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
