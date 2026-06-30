using System.Diagnostics;
using Line.Framework.Input;
using Veldrid;
using static SDL3.SDL;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIButton : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 0);
    public event EventHandler<UIButton, byte> WhenPress;
    public event EventHandler<UIButton, byte> WhenClick;
    public int ClickMaximumTime { get; set; } = 200;
    Stopwatch ClickSw = new();

    public event EventHandler<UIButton, byte> WhenRelease;
    public bool clicking { get; private set; } = false;
    public bool enabled { get; set; } = true;
    private InputManager input;
    Action<RendererContextArgs> RenderAction;

    public override void RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    public UIButton()
    {
        Press = (a) =>
        {
            if (visible && enabled && HitTest(input.TotalMouseDelta))
            {
                WhenPress?.Invoke(this, a);
                clicking = true;
                ClickSw.Reset();
                ClickSw.Restart();
            }
        };
        Release = (a) =>
        {
            if (clicking)
            {
                clicking = false;
                WhenRelease?.Invoke(this, a);
                ClickSw.Stop();
                if (ClickSw.Elapsed.Milliseconds <= ClickMaximumTime)
                {
                    WhenClick?.Invoke(this, a);
                }
            }
        };
        UpdateRoot();
        RenderAction = (RendererContextArgs args) =>
        {
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
            {
                return;
            }
            var collector = args.Collector;
            collector.DrawRect(
                new Rectangle
                {
                    X = 0,
                    Y = 0,
                    Height = (float)args.height,
                    Width = (float)args.width,
                },
                0,
                Anchor,
                color,
                this
            );
        };
    }

    public void UpdateRoot()
    {
        if (input != null)
        {
            input.MouseDown -= (a) =>
            {
                Press(a.Button);
            };
            input.MouseUp -= (a) =>
            {
                Release(a.Button);
            };
        }
        var a = FindRoot(this);
        if (a is UIScreen)
        {
            var b = a as UIScreen;
            input = b.window.Input;
            input.MouseDown += (a) =>
            {
                Press(a.Button);
            };
            input.MouseUp += (a) =>
            {
                Release(a.Button);
            };
        }
    }

    public override void SetParent(UINode value)
    {
        base.SetParent(value);
        UpdateRoot();
    }

    Action<byte> Press { get; init; }
    Action<byte> Release { get; init; }
}
