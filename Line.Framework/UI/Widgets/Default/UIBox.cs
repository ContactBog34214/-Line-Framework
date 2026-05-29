using Line.Framework.Graphics;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIBox : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 1);

    public UIBox()
    {
        RendererContext = (RendererContextArgs args) =>
        {
            var collector = args.Collector;
            /*
            collector.DrawRect(
                new Rectangle
                {
                    X = 0,
                    Y = 0,
                    Height = (float)args.height,
                    Width = (float)args.width,
                },
                0,
                anchor,
                color,
                this
            );
            */
            var tl = new WindowsRenderer.Vertex(
                new(0, 0),
                color,
                new(new(), new(0, 0)),
                null,
                null,
                1
            );
            var tr = new WindowsRenderer.Vertex(
                new((float)args.height, 0),
                color,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = new WindowsRenderer.Vertex(
                new(0, (float)args.width),
                color,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = new WindowsRenderer.Vertex(
                new((float)args.height, (float)args.width),
                color,
                new(new(), new(1, 1)),
                null,
                null,
                1
            );
            collector.DrawVertex([tl, tr, bl], this);
            collector.DrawVertex([tr, bl, br], this);
        };
    }
}
