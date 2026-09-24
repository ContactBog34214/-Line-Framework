using Line.Framework.Graphics;
using Line.Framework.UI;
using Veldrid;

namespace Line.Framework.Default.UIWidgets;

public class UIBox : UIWidget
{
    public Types.RgbaFloat color { get; set; } = new(0, 0, 0, 1f);
    readonly Action<RendererContextArgs> RenderAction;

    public override async Task RendererContext(RendererContextArgs args)
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
            var tl = Vertex.New(
                new(0, 0),
                color,
                new(new(), new(0, 0)),
                null,
                null,
                1
            );
            var tr = Vertex.New(
                new((float)args.width, 0),
                color,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = Vertex.New(
                new(0, (float)args.height),
                color,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = Vertex.New(
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
