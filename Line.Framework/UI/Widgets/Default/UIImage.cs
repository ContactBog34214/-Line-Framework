using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Graphic;
using Veldrid;

namespace Line.Framework.UI.DefaultWidget;

public class UIImage : UIWidget
{
    public RgbaFloat BackgroundColor { get; set; } = new(0, 0, 0, 0);
    public RgbaFloat Color { get; set; } = new(1, 1, 1, 1f);
    public string TextureId { get; set; }
    internal ResourceManager Manager;
    readonly Action<RendererContextArgs> RenderAction;

    public override void RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    public UIImage(ResourceManager manager)
    {
        Manager = manager;
        RenderAction = (RendererContextArgs args) =>
        {
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
            {
                return;
            }
            var collector = args.Collector;

            //背景
            var tl = new Vertex(
                new(0, 0),
                BackgroundColor,
                new(new(), new(0, 0)),
                null,
                null,
                1
            );
            var tr = new Vertex(
                new((float)args.width, 0),
                BackgroundColor,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = new Vertex(
                new(0, (float)args.height),
                BackgroundColor,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = new Vertex(
                new((float)args.width, (float)args.height),
                BackgroundColor,
                new(new(), new(1, 1)),
                null,
                null,
                1
            );
            collector.DrawVertex([tl, tr, bl], this);
            collector.DrawVertex([tr, bl, br], this);

            //纹理
            var Resource = Manager.GetResource(TextureId) as ResourceSetArg;
            if (Resource == null)
                return;
            var ResourceSet = Resource.ResourceSet;
            var Texture = Resource.Texture;
            if (ResourceSet == null)
                return;

            var ttl = new Vertex(
                new(0, 0),
                Color,
                new(new(), new(0, 0)),
                Texture,
                ResourceSet,
                1
            );
            var ttr = new Vertex(
                new((float)args.height, 0),
                Color,
                new(new(), new(1, 0)),
                Texture,
                ResourceSet,
                1
            );
            var tbl = new Vertex(
                new(0, (float)args.width),
                Color,
                new(new(), new(0, 1)),
                Texture,
                ResourceSet,
                1
            );
            var tbr = new Vertex(
                new((float)args.height, (float)args.width),
                Color,
                new(new(), new(1, 1)),
                Texture,
                ResourceSet,
                1
            );
            collector.DrawVertex([ttl, ttr, tbl], this);
            collector.DrawVertex([ttr, tbl, tbr], this);
        };
    }
}
