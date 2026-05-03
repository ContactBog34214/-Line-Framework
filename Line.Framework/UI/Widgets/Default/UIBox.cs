using System.Numerics;
using Line.Framework.UI;

namespace Line.Framework.UI.DefaultWidget;

public class UIBox : UIWidget
{
    public Vector3 color = new Vector3(255, 255, 255);

    public UIBox()
    {
        this.RendererContext = ((args) => { });
    }
}
