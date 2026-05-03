using System.Numerics;
using Veldrid;

namespace Line.Framework.UI;

public class UIWidget : UINode
{
    public Coord2 Position = new Coord2();
    public Coord2 Size = new Coord2();
    public Vector2 anchor = new Vector2(0, 0);
    public bool visible = true;
    public Texture RenderTexture { get; private set; }
    public Framebuffer RenderFramebuffer { get; private set; }

    public class RendererContextArgs : EventArgs
    {
        public double X;
        public double Y;
        public double width;
        public double height;
    }

    public Action<RendererContextArgs> RendererContext;
    public float z = 0;
    public double rotation = 0;
    public float Opacity = 0;
}
