using Line.Framework.Graphics;
using Veldrid;
using Veldrid.ImageSharp;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIImage : UIWidget
{
    public RgbaFloat BackgroundColor { get; set; } = new(0, 0, 0, 0);
    public RgbaFloat Color { get; set; } = new(1, 1, 1, 1);
    public Texture Texture { get; set; }
    ResourceSet ResourceSet;

    public void LoadImage(GraphicsDevice gd, ResourceLayout rl, string path)
    {
        try
        {
            var image = new ImageSharpTexture(path);
            Texture = image.CreateDeviceTexture(gd, gd.ResourceFactory);
            ResourceSet = gd.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(rl, Texture)
            );
        }
        catch (FileNotFoundException)
        {
            Log.Warning($"[LoadImage]Could not find file {path}");
        }
        catch (Exception e)
        {
            Log.Error($"[LoadImage]{e}");
        }
    }

    public void LoadTexture(GraphicsDevice gd, ResourceLayout rl, Texture t)
    {
        try
        {
            Texture = t;
            ResourceSet = gd.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(rl, Texture)
            );
        }
        catch (Exception e)
        {
            Log.Error($"[LoadTexture]{e}");
        }
    }

    public UIImage()
    {
        DisposeHook = () => ResourceSet.Dispose();
        RendererContext = (RendererContextArgs args) =>
        {
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
            {
                return;
            }
            var collector = args.Collector;

            //背景
            var tl = new WindowsRenderer.Vertex(
                new(0, 0),
                BackgroundColor,
                new(new(), new(0, 0)),
                null,
                null,
                1
            );
            var tr = new WindowsRenderer.Vertex(
                new((float)args.height, 0),
                BackgroundColor,
                new(new(), new(1, 0)),
                null,
                null,
                1
            );
            var bl = new WindowsRenderer.Vertex(
                new(0, (float)args.width),
                BackgroundColor,
                new(new(), new(0, 1)),
                null,
                null,
                1
            );
            var br = new WindowsRenderer.Vertex(
                new((float)args.height, (float)args.width),
                BackgroundColor,
                new(new(), new(1, 1)),
                null,
                null,
                1
            );
            collector.DrawVertex([tl, tr, bl], this);
            collector.DrawVertex([tr, bl, br], this);

            //纹理

            if (Texture == null)
                return;
            if (ResourceSet == null)
                return;

            var ttl = new WindowsRenderer.Vertex(
                new(0, 0),
                Color,
                new(new(), new(0, 0)),
                Texture,
                ResourceSet,
                1
            );
            var ttr = new WindowsRenderer.Vertex(
                new((float)args.height, 0),
                Color,
                new(new(), new(1, 0)),
                Texture,
                ResourceSet,
                1
            );
            var tbl = new WindowsRenderer.Vertex(
                new(0, (float)args.width),
                Color,
                new(new(), new(0, 1)),
                Texture,
                ResourceSet,
                1
            );
            var tbr = new WindowsRenderer.Vertex(
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
