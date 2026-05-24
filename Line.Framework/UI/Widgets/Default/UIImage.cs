using Veldrid;
using Veldrid.ImageSharp;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIImage : UIWidget
{
    public RgbaFloat BackgroundColor { get; set; } = new(0, 0, 0, 1);
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
            Log.Debug($"Loaded Image{path}");
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
            Log.Debug($"[LoadTexture]Loaded Texture{t.Name}");
        }
        catch (Exception e)
        {
            Log.Error($"[LoadTexture]{e}");
        }
    }

    public UIImage()
    {
        RendererContext = (RendererContextArgs args) =>
        {
            var collector = args.Collector;
            if (Texture == null)
                return;
            if (ResourceSet == null)
                return;
            args.Collector.DrawTexture(
                new Rectangle(0, 0, (float)args.width, (float)args.height),
                rotation,
                anchor,
                ResourceSet,
                Texture,
                BackgroundColor,
                this
            );
        };
    }
}
