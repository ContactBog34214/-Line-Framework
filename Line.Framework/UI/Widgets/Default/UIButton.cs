using System.Diagnostics;
using System.Numerics;
using Line.Framework.Input;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI.DefaultWidget;

public class UIButton : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 0);
    public event EventHandler<UIButton, MouseButton> WhenPress;
    public event EventHandler<UIButton, MouseButton> WhenClick;
    public int ClickMaximumTime { get; set; } = 200;
    Stopwatch ClickSw = new();

    public event EventHandler<UIButton, MouseButton> WhenRelease;
    public bool clicking { get; private set; } = false;
    public bool enabled { get; set; } = true;
    private InputManager input;

    public UIButton()
    {
        Press = (a) =>
        {
            if (
                visible
                && enabled
                && HitTest(
                    GetPositionOnScreen(),
                    new(Size.offset.X + Size.scale.X * s.X, Size.offset.Y + Size.scale.Y * s.Y),
                    input.TotalMouseDelta
                )
            )
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
                0,
                anchor,
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
                Press(a);
            };
            input.MouseUp -= (a) =>
            {
                Release(a);
            };
        }
        var a = FindRoot(this);
        if (a is UIScreen)
        {
            var b = a as UIScreen;
            input = b.window.Input;
            input.MouseDown += (a) =>
            {
                Press(a);
            };
            input.MouseUp += (a) =>
            {
                Release(a);
            };
        }
    }

    Action<MouseButton> Press { get; init; }
    Action<MouseButton> Release { get; init; }

    public static UINode FindRoot(UINode widget)
    {
        if (widget.parent != null)
        {
            return FindRoot(widget.parent);
        }
        else if (widget is UIScreen)
        {
            return widget as UINode;
        }
        else
        {
            return null;
        }
    }
}
