using Veldrid;

namespace Line.Framework.UI.DefaultWidget;

public class UIButton : UIWidget
{
    public RgbaFloat BackgroundColor { get; set; } = new(0, 0, 0, 0);
    public RgbaFloat TextColor { get; set; } = new(1, 1, 1, 1);
    public string Text { get; set; } = "Button";
    public event EventHandler<UIButton> WhenClick;
    public event EventHandler<UIButton> WhenRelease;

    public UIButton() { }
}
