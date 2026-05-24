using Veldrid;
using Veldrid.ImageSharp;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIImage : UIWidget
{
    public RgbaFloat BackgroundColor { get; set; } = new(0, 0, 0, 1);
    public Texture Texture { get; private set; }
    ResourceSet _resourceSet;

    public void LoadImage(GraphicsDevice gd, ResourceLayout rl, string path)
    {
        var image = new ImageSharpTexture(path);
        Texture = image.CreateDeviceTexture(gd, gd.ResourceFactory);
        _resourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(rl, Texture));
    }

    public UIImage()
    {
        RendererContext = (RendererContextArgs args) =>
        {
            var collector = args.Collector;
            if (Texture == null)
                return;
            if (_resourceSet == null)
                return;
            args.Collector.DrawTexture(
                new Rectangle(0, 0, (float)args.width, (float)args.height),
                rotation,
                anchor,
                _resourceSet,
                Texture,
                BackgroundColor,
                this
            );
        };
    }
        public new void Dispose()
    {
        _resourceSet?.Dispose();
        Texture?.Dispose();
    }
}
