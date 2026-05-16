using System.Numerics;

namespace Line.Framework.UI;

public class UIWidget : UINode
{
    public Coord2 Position { get; set; } = new();
    public Coord2 Size { get; set; } = new();
    public Vector2 anchor { get; set; } = new(0, 0);
    public bool visible { get; set; } = true;

    public Action<RendererContextArgs> RendererContext;
    public float z { get; set; } = 0;
    public float rotation { get; set; } = 0;
    public float Opacity { get; set; } = 1;
    public Vector2 s { get; set; } = new(0, 0);
    public Vector2 p { get; set; } = new(0, 0);
    public float o{get;set;}=1;
}

public class RendererContextArgs
{
    public double X { get; set; }
    public double Y { get; set; }
    public double width { get; set; }
    public double height { get; set; }
    public UIDrawCollector Collector { get; set; }
}
