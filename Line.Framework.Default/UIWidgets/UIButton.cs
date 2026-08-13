using System.Diagnostics;
using Line.Framework.IO;
using Line.Framework.UI;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.Default.UIWidgets;

public class UIButton : UIWidget
{
    public RgbaFloat color { get; set; } = new(0, 0, 0, 0f);
    public event EventHandler<UIButton, ICursor> WhenPress;
    public event EventHandler<UIButton, ICursor> WhenClick;
    public int ClickMaximumTime { get; set; } = 200;
    Stopwatch ClickSw = new();

    public event EventHandler<UIButton, ICursor> WhenRelease;
    public bool Clicking { get; private set; } = false;
    public bool Enabled { get; set; } = true;
    private InputManager input;
    readonly Action<RendererContextArgs> RenderAction;

    public override void Dispose()
    {
        SetParent(null);
        base.Dispose();
    }

    public override async Task RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    List<ICursor> Pressing = [];

    void UpdateState(ICursor cursor)
    {
        if (Clicking != (Pressing.Count != 0))
        {
            Clicking = Pressing.Count != 0;
            if (Clicking)
            {
                WhenPress?.Invoke(this, cursor);
                ClickSw.Reset();
                ClickSw.Restart();
            }
            else
            {
                WhenRelease?.Invoke(this, cursor);
                ClickSw.Stop();
                if (ClickSw.Elapsed.Milliseconds <= ClickMaximumTime)
                {
                    WhenClick?.Invoke(this, cursor);
                }
            }
        }
    }

    public UIButton()
    {
        Press = (a) =>
        {
            if (Visible && Enabled && IsWidgetPointTouched(root, this, input.Mouse.Position))
            {
                Pressing.Add(a);
                UpdateState(a);
            }
        };
        Release = (a) =>
        {
            Pressing.Remove(a);
            UpdateState(a);
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
                color,
                this
            );
        };
    }

    public void UpdateRoot()
    {
        if (input != null)
        {
            input?.CursorDown -= Press;
            input?.CursorUp -= Release;
        }
        var a = FindRoot(this);
        if (a is UIScreen)
        {
            var b = a as UIScreen;
            input = b.InputManager;
            input?.CursorDown += Press;
            input?.CursorUp += Release;
        }
        root = a as UIWidget;
    }

    UIWidget root;

    public override void SetParent(UINode value)
    {
        base.SetParent(value);
        UpdateRoot();
    }

    Action<ICursor> Press { get; init; }
    Action<ICursor> Release { get; init; }
}
