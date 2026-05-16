using System.Numerics;
using System.Text;
using Line.Framework.Graphics;
using Veldrid;
using Veldrid.OpenGLBinding;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;
using Vortice.Mathematics;
using Vulkan.Xlib;
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
                    X = (float)args.X,
                    Y = (float)args.Y,
                    Height = (float)args.height,
                    Width = (float)args.width,
                },
                rotation,
                anchor,
                color,
                Opacity,
                this
            );
        };
    }
}
