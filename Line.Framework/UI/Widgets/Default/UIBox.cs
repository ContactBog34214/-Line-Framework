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
        };
    }
}
