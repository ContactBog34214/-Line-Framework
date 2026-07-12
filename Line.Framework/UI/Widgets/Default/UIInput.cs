using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Input;
using Line.Framework.Resource;
using Veldrid;
using SDL3;

namespace Line.Framework.UI.DefaultWidget;

public class UIInput : UIWidget
{
    public bool Focused
    {
        get => Focus == this;
        set
        {
            if (Focused == value)
                return;
            if (value)
                Focus = this;
            else if (Focus == this)
                Focus = null;
            if (Focus != null)
                SDL.StartTextInput((root as UIScreen)?.window.WindowHandle ?? nint.Zero);
            else
                SDL.StopTextInput((root as UIScreen)?.window.WindowHandle ?? nint.Zero);
            if (Focused)
            {
                (root as UIScreen)?.window.EventPool.TryAdd(SDL.EventType.TextInput, InputAction);
                SDL.RaiseWindow((root as UIScreen)?.window.WindowHandle ?? nint.Zero);
            }
            else
                (root as UIScreen)?.window.EventPool.TryRemove(
                    SDL.EventType.TextInput,
                    out InputAction
                );
        }
    }

    public record Cursor(int StartPosition, int EndPosition);

    readonly Action<KeyCode> KeyAction;

    void WhenKeyDown(KeyCode ev)
    {
        if (!(im?.IsKeyDown(ev) ?? false))
            return;
        if (!Focused)
            return;
        if (!Enabled)
            return;
        switch (ev)
        {
            case KeyCode.Escape:
                Focused = false;
                break;
            case KeyCode.Backspace:
                if (InputPosition.StartPosition == InputPosition.EndPosition)
                {
                    if (InputPosition.StartPosition <= 0)
                        return;
                    if (Text.Length == 0)
                        return;
                    string front = Text.Substring(0, InputPosition.StartPosition - 1);
                    string back = Text.Substring(InputPosition.EndPosition);
                    Text = front + back;
                    var cur = front.Length;
                    InputPosition = new(cur, cur);
                }
                else
                {
                    if (InputPosition.StartPosition <= 0)
                        return;
                    if (Text.Length == 0)
                        return;
                    string front = Text.Substring(0, InputPosition.StartPosition);
                    string back = Text.Substring(InputPosition.EndPosition);
                    Text = front + back;
                    var cur = front.Length;
                    InputPosition = new(cur, cur);
                }
                break;
            case KeyCode.V:
                if (
                    (im?.IsKeyDown(KeyCode.LCtrl) ?? false)
                    || (im?.IsKeyDown(KeyCode.RCtrl) ?? false)
                )
                    AddText(im?.GetClipBoardText() ?? "");
                break;
            case KeyCode.Left:
                var cur2 = InputPosition.StartPosition;
                if (InputPosition.StartPosition == InputPosition.EndPosition)
                {
                    cur2--;
                }
                InputPosition = new(cur2, cur2);
                break;
            case KeyCode.Right:
                var cur3 = InputPosition.EndPosition;
                if (InputPosition.StartPosition == InputPosition.EndPosition)
                {
                    cur3++;
                }
                InputPosition = new(cur3, cur3);
                break;
        }
    }

    Action<SDL.Event> InputAction;

    void WhenInput(SDL.Event ev)
    {
        if (!Enabled)
            return;
        string inputText = Marshal.PtrToStringUTF8(ev.Text.Text);
        AddText(inputText);
    }

    void AddText(string t)
    {
        if (t.Length == 0)
            return;
        string front = Text.Substring(0, InputPosition.StartPosition);
        string back = Text.Substring(InputPosition.EndPosition);
        var cur = front.Length + t.Length;
        Text = front + t + back;
        InputPosition = new(cur, cur);
    }

    public Cursor InputPosition
    {
        get;
        set
        {
            var s = value.StartPosition;
            var e = value.EndPosition;
            if (e < s)
                e = s;
            if (e > Text.Length)
                e = Text.Length;
            if (s > Text.Length)
                s = Text.Length;
            if (e < 0)
                e = 0;
            if (s < 0)
                s = 0;
            field = new(s, e);
        }
    } = new(0, 0);

    public string Text { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Hint { get; set; } = "Type something...";
    public RgbaFloat CursorColor { get; set; } = new(1, 1, 1, 1);
    public float CursorWidth { get; set; } = 5;
    public string FontId { get; set; } = "";
    public RgbaFloat TextColor { get; set; } = new(1, 1, 1, 1);
    public RgbaFloat HintColor { get; set; } = new(0.7f, 0.7f, 0.7f, 0.5f);
    public float FontScale { get; set; } = 1;
    readonly UIText TextWidget;
    readonly UIText HintWidget;

    public UIInput(ResourceManager rm)
    {
        ClickAction = a => WhenClick(new(a.Position.X,a.Position.Y));
        InputAction = WhenInput;
        KeyAction = WhenKeyDown;
        if (rm == null)
        {
            throw new NullReferenceException();
        }
        TextWidget = new(rm);
        HintWidget = new(rm);
        SyncChildrenAtt();
    }

    InputManager im;
    UIWidget root;

    public override void SetParent(UINode value)
    {
        var i = Focused;
        if (Parent != value)
            Focused = false;
        base.SetParent(value);
        root = FindRoot(this) as UIWidget;
        if ((root as UIScreen)?.window.Input == im)
            return;
        im?.CursorDown -= ClickAction;
        im?.KeyDown -= KeyAction;
        im = (root as UIScreen)?.window.Input;
        im?.CursorDown += ClickAction;
        im?.KeyDown += KeyAction;

        Focused = i;
    }

    public static UIInput Focus { get; internal set; }

    readonly Action<ICursor> ClickAction;

    public bool FadeWhenNotInput { get; set; } = true;

    void WhenClick(Vector2 pos)
    {
        var t = FindWidgetPointTouched(root, pos);
        Focused = t == this && Enabled;
    }

    void SyncChildrenAtt()
    {
        TextWidget?.color = TextColor;
        HintWidget?.color = HintColor;
        TextWidget?.FontId = FontId;
        HintWidget?.FontId = FontId;
        TextWidget?.FontScale = FontScale;
        HintWidget?.FontScale = FontScale;
        TextWidget?.Text = Text;
        HintWidget?.Text = Hint;
    }

    public override void RendererContext(RendererContextArgs args)
    {
        SyncChildrenAtt();
        base.RendererContext(args);
        UIDrawCollector collector = new();
        bool usingHint = (Text?.Length ?? 0) == 0;
        RendererContextArgs Args = new()
        {
            X = args.X,
            Y = args.Y,
            width = args.width,
            height = args.height,
            Collector = collector,
        };
        if (usingHint)
        {
            var s = HintWidget?.GetTextSize(Hint) ?? new(0, 0);
            HintWidget?.Offset = new(0, s.Y * 0.05f);
            HintWidget?.RendererContext(Args);
        }
        else
        {
            var s = TextWidget?.GetTextSize(Text) ?? new(0, 0);
            TextWidget?.Offset = new(0, s.Y * 0.05f);
            TextWidget?.RendererContext(Args);
        }
        var cl = args.Collector;
        foreach (var i in collector.Verts.Select(i => i.Vert))
        {
            cl.DrawVertex(i, this);
        }

        void DrawSelectArea(Cursor cursor, RgbaFloat color)
        {
            var st = cursor.StartPosition;
            var e = cursor.EndPosition;
            if (e < st)
                e = st;
            if (e > Text.Length)
                e = Text.Length;
            if (st > Text.Length)
                st = Text.Length;
            if (e < 0)
                e = 0;
            if (st < 0)
                st = 0;
            cursor = new(st, e);
            string[] AllLinesBeforeCur;
            if (cursor.StartPosition >= 0)
            {
                var sub = Text.Substring(0, cursor.StartPosition);
                AllLinesBeforeCur = sub.Split('\n');
                var s = TextWidget?.GetTextSize(sub) ?? new();
                var EndHeight = s.Y;
                var Height =
                    TextWidget?.GetTextSize(AllLinesBeforeCur[AllLinesBeforeCur.Length - 1]).Y ?? 0;
                if (EndHeight - Height == 0)
                {
                    Height = EndHeight = TextWidget?.GetTextSize(" ").Y ?? 0;
                }

                var sub2 = Text.Substring(0, cursor.EndPosition);
                AllLinesBeforeCur = sub2.Split('\n');
                var s2 = TextWidget?.GetTextSize(sub2) ?? new();
                var EndHeight2 = s2.Y;
                var Height2 =
                    TextWidget?.GetTextSize(AllLinesBeforeCur[AllLinesBeforeCur.Length - 1]).Y ?? 0;
                if (EndHeight2 - Height2 == 0)
                {
                    Height2 = EndHeight2 = TextWidget?.GetTextSize(" ").Y ?? 0;
                }
                if (EndHeight == EndHeight2)
                {
                    float[] h = [Height, Height2];
                    cl.DrawRect(
                        new(s.X, EndHeight - h.Max(), s2.X + CursorWidth - s.X, h.Max()),
                        0,
                        new(0),
                        color,
                        this
                    );
                }
                else
                {
                    if (EndHeight != EndHeight2 - Height2)
                        cl.DrawRect(
                            new(0, EndHeight, (float)args.width, EndHeight2 - Height2 - EndHeight),
                            0,
                            new(0),
                            color,
                            this
                        );
                    cl.DrawRect(
                        new(s.X, EndHeight - Height, (float)args.width - s.X, Height),
                        0,
                        new(0),
                        color,
                        this
                    );
                    cl.DrawRect(
                        new(0, EndHeight2 - Height2, s2.X + CursorWidth, Height),
                        0,
                        new(0),
                        color,
                        this
                    );
                }
            }
        }

        if (Focused && Text != null)
        {
            Vector4 color = new(CursorColor.R, CursorColor.G, CursorColor.B, CursorColor.A);
            if (!SDL.TextInputActive((root as UIScreen)?.window.WindowHandle ?? nint.Zero))
                color.W *= 0.3f;
            DrawSelectArea(InputPosition, new(color.X, color.Y, color.Z, color.W));
        }
    }

    public override void Dispose()
    {
        Focused = false;
        base.Dispose();
        TextWidget?.Dispose();
        HintWidget?.Dispose();
    }
}
