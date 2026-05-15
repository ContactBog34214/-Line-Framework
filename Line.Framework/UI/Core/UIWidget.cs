using System.Numerics;
using Veldrid;

namespace Line.Framework.UI;

public class UIWidget : UINode
{
    public Vector2 Position = new(0, 0);
    public Vector2 Size = new(0, 0);
    public Vector2 anchor = new(0, 0);
    public bool visible = true;

    public class RendererContextArgs : EventArgs
    {
        public double X;
        public double Y;
        public double width;
        public double height;
        public CommandList command;
    }

    public Action<RendererContextArgs> RendererContext;
    public float z = 0;
    public float rotation = 0;
    public float Opacity = 0;
}
