using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.UI.DefaultWidget;

public class UIBox : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 1f);
    readonly Action<RendererContextArgs> RenderAction;

    public override void RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    public UIBox()
    {
        RenderAction = (RendererContextArgs args) =>
        {
            var collector = args.Collector;
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
            {
                return;
            }
            var tl = new Vertex(
                new(0, 0),
                color,
                new(new(), new(0, 0)),
                null,
                null,
                1
            );
            var tr = new Vertex(
                new((float)args.width, 0),
                color,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = new Vertex(
                new(0, (float)args.height),
                color,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = new Vertex(
                new((float)args.width, (float)args.height),
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
