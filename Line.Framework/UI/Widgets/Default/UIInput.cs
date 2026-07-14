using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.Input;
using Line.Framework.Resource;
using SDL3;
using Veldrid;

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

    void WhenKeyDown(KeyCode ev)
    {
        if (!(im?.IsKeyDown(ev) ?? false))
            return;
        if (!Focused)
            return;
        if (!Enabled)
            return;
        bool shift =
            (im?.IsKeyDown(KeyCode.LShift) ?? false) || (im?.IsKeyDown(KeyCode.RShift) ?? false);
        bool ctrl =
            (im?.IsKeyDown(KeyCode.LCtrl) ?? false) || (im?.IsKeyDown(KeyCode.RCtrl) ?? false);
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
                    if (InputPosition.StartPosition < 0)
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
                if (ctrl)
                    AddText(im?.GetClipBoardText() ?? "");
                break;
            case KeyCode.Left:
                var Loffset = 1;
                if (shift)
                {
                    if (!IsLeftMain)
                    {
                        InputPosition = new(
                            InputPosition.StartPosition - Loffset,
                            InputPosition.EndPosition
                        );
                    }
                    else
                    {
                        int[] l =
                        [
                            InputPosition.StartPosition,
                            InputPosition.EndPosition - Loffset,
                        ];
                        InputPosition = new(l.Min(), l.Max());
                        if (l[0] >= l[1])
                            IsLeftMain = false;
                    }
                    break;
                }
                var cur2 = InputPosition.StartPosition;
                if (InputPosition.StartPosition == InputPosition.EndPosition)
                {
                    cur2--;
                }
                InputPosition = new(cur2, cur2);
                break;
            case KeyCode.Right:
                var Roffset = 1;
                if (shift)
                {
                    if (IsLeftMain)
                    {
                        InputPosition = new(
                            InputPosition.StartPosition,
                            InputPosition.EndPosition + Roffset
                        );
                    }
                    else
                    {
                        int[] l =
                        [
                            InputPosition.StartPosition + Roffset,
                            InputPosition.EndPosition,
                        ];
                        InputPosition = new(l.Min(), l.Max());
                        if (l[0] == l[1])
                            IsLeftMain = true;
                    }
                    break;
                }
                var cur3 = InputPosition.EndPosition;
                if (InputPosition.StartPosition == InputPosition.EndPosition)
                {
                    cur3++;
                }
                InputPosition = new(cur3, cur3);
                break;
            case KeyCode.KpEnter:
            case KeyCode.Return:
                if (LineBreaks)
                    AddText("\n");
                break;
            case KeyCode.A:
                if (ctrl)
                {
                    IsLeftMain = true;
                    InputPosition = new(0, Text.Length);
                }
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

    void WhenScroll(IMouse mouse)
    {
        if (!CanScrollByCursor)
            return;
        if (!HitTest(mouse.Position))
            return;
        var of = Offset;
        of.Y -= mouse.WheelDelta.Y * ScrollRate;
        of.X += mouse.WheelDelta.X * ScrollRate;
        Offset = of;
    }

    public uint ScrollRate { get; set; } = 15;
    public bool CanScrollByCursor { get; set; } = true;

    void AddText(string t)
    {
        if (t.Length == 0)
            return;
        string front = Text.Substring(0, InputPosition.StartPosition);
        string back = Text.Substring(InputPosition.EndPosition);
        var cur = front.Length + t.Length;
        Text = front + t + back;
        InputPosition = new(cur, cur);
        SetOffsetToDefault();
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
            SetOffsetToDefault();
        }
    } = new(0, 0);

    public string Text { get; set; } = "";

    public void SetOffsetToDefault()
    {
        var idx = 0;

        if (IsLeftMain)
            idx += InputPosition.EndPosition;
        else
            idx += InputPosition.StartPosition;
        Vector2 pos = TextWidget?.GetWhereIndexCharIs(Text, idx) ?? new();
        var s = GetSizeOnScreen();
        var top = 0;
        var bottom = s.Y;
        var left = 0;
        var right = s.X;

        var Height = (TextWidget?.GetTextSize(" ") ?? new()).Y;
        if (top > (pos - Offset).Y || bottom < pos.Y + Height - Offset.Y)
        {
            if (top > (pos - Offset).Y)
                Offset = new(Offset.X, pos.Y);
            else
                Offset = new(Offset.X, pos.Y + Height - s.Y);
        }
        if (left > pos.X - Offset.X || right < pos.X - Offset.X)
        {
            if (left > pos.X - Offset.X)
                Offset = new(pos.X, Offset.Y);
            else
                Offset = new(pos.X - s.X + CursorWidth, Offset.Y);
        }
    }

    public bool AllowPaste { get; set; } = true;
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
    ICursor pressed = null;

    void WhenHold(ICursor a)
    {
        if (pressed == null)
            return;
        var curIdx = GetIndexOnCur(a.Position);
        if (IsLeftMain)
        {
            IsLeftMain = curIdx >= InputPosition.StartPosition;
            if (curIdx == InputPosition.StartPosition)
                InputPosition = new(curIdx, curIdx);
            if (curIdx < InputPosition.StartPosition)
                InputPosition = new(curIdx, InputPosition.StartPosition);
            else
                InputPosition = new(InputPosition.StartPosition, curIdx);
        }
        else
        {
            IsLeftMain = curIdx >= InputPosition.EndPosition;
            if (curIdx == InputPosition.EndPosition)
                InputPosition = new(curIdx, curIdx);
            if (curIdx > InputPosition.EndPosition)
                InputPosition = new(InputPosition.EndPosition, curIdx);
            else
                InputPosition = new(curIdx, InputPosition.EndPosition);
        }
    }

    public UIInput(ResourceManager rm)
    {
        InputAction = WhenInput;
        if (rm == null)
        {
            throw new NullReferenceException();
        }
        TextWidget = new(rm);
        HintWidget = new(rm);
        SyncChildrenAtt();
    }

    public int GetIndexOnCur(Vector2 cur)
    {
        SyncChildrenAtt();
        var tmp = MousePosition(cur);
        List<string> lines = Text.Split('\n').ToList();
        float topY = 0;
        float bottomY = -Offset.Y;
        string Select = "";
        int Result = 0;
        if (tmp.Y < topY)
            return 0;
        else
            for (int i = 0; i < lines.Count; i++)
            {
                topY = bottomY;
                bottomY += (TextWidget?.GetTextSize(lines[i]) ?? new(0, 0)).Y;
                Result += lines[i].Length;
                if (tmp.Y >= topY && bottomY >= tmp.Y)
                {
                    Select = lines[i];
                    Result -= lines[i].Length;
                    if (i != 0)
                        Result += i;
                    break;
                }
                if (i + 1 == lines.Count && bottomY < tmp.Y)
                    return Text.Length;
            }
        float startX = -Offset.X;
        if (tmp.X < startX)
            return Result;
        else
            for (int i = 0; i < Select.Length; i++)
            {
                var l = (TextWidget?.GetTextSize(Select.Substring(i, 1)) ?? new(0, 0)).X;

                if (startX <= tmp.X && startX + l >= tmp.X)
                {
                    break;
                }
                startX += l;
                Result++;
            }
        return Result;
    }

    InputManager im;

    public Vector2 Offset { get; set; } = new(0);
    UIWidget root;

    public override void SetParent(UINode value)
    {
        var i = Focused;
        if (Parent != value)
        {
            Focused = false;
            pressed = null;
        }
        base.SetParent(value);
        root = FindRoot(this) as UIWidget;
        if ((root as UIScreen)?.window.Input == im)
            return;
        im?.CursorDown -= WhenClick;
        im?.CursorUp -= WhenUp;
        im?.KeyDown -= WhenKeyDown;
        im?.CursorMove -= WhenHold;
        im?.MouseWheel -= WhenScroll;
        im = (root as UIScreen)?.window.Input;
        im?.CursorDown += WhenClick;
        im?.KeyDown += WhenKeyDown;
        im?.CursorUp += WhenUp;
        im?.CursorMove += WhenHold;
        im?.MouseWheel += WhenScroll;

        Focused = i;
    }

    public static UIInput Focus { get; internal set; }

    public bool FadeWhenNotInput { get; set; } = true;
    public bool IsLeftMain { get; set; } = true;
    public bool LineBreaks { get; set; } = true;

    void WhenClick(ICursor cursor)
    {
        var pos = cursor.Position;
        var t = FindWidgetPointTouched(root, pos);
        Focused = t == this && Enabled;
        if (Focused)
        {
            pressed = cursor;
            var cur = GetIndexOnCur(pos);
            InputPosition = new(cur, cur);
            IsLeftMain = true;
        }
    }

    void WhenUp(ICursor cursor)
    {
        if (cursor == pressed)
            pressed = null;
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
            var s = HintWidget?.GetTextSize(" ") ?? new(0, 0);
            HintWidget?.Offset = new(-Offset.X, s.Y * 0.1f - Offset.Y);
            HintWidget?.RendererContext(Args);
        }
        else
        {
            var s = TextWidget?.GetTextSize(" ") ?? new(0, 0);
            TextWidget?.Offset = new(-Offset.X, s.Y * 0.1f - Offset.Y);
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
                var s = TextWidget?.GetTextSize(AllLinesBeforeCur.Last()) ?? new();
                var EndHeight = (TextWidget?.GetTextSize(sub) ?? new()).Y;

                var Height =
                    TextWidget?.GetTextSize(AllLinesBeforeCur[AllLinesBeforeCur.Length - 1]).Y ?? 0;
                if (EndHeight - Height == 0)
                {
                    Height = EndHeight = TextWidget?.GetTextSize(" ").Y ?? 0;
                }
                else if (Height == 0)
                {
                    Height = TextWidget?.GetTextSize(" ").Y ?? 0;
                }

                var sub2 = Text.Substring(0, cursor.EndPosition);
                AllLinesBeforeCur = sub2.Split('\n');
                var s2 = TextWidget?.GetTextSize(AllLinesBeforeCur.Last()) ?? new();
                var EndHeight2 = (TextWidget?.GetTextSize(sub2) ?? new()).Y;

                var Height2 =
                    TextWidget?.GetTextSize(AllLinesBeforeCur[AllLinesBeforeCur.Length - 1]).Y ?? 0;
                if (EndHeight2 - Height2 == 0)
                {
                    Height2 = EndHeight2 = TextWidget?.GetTextSize(" ").Y ?? 0;
                }
                else if (Height2 == 0)
                {
                    Height2 = TextWidget?.GetTextSize(" ").Y ?? 0;
                }
                if (EndHeight == EndHeight2)
                {
                    float[] h = [Height, Height2];
                    cl.DrawRect(
                        new(
                            s.X - Offset.X,
                            EndHeight - h.Max() - Offset.Y,
                            Math.Abs(s2.X - s.X) + CursorWidth,
                            h.Max()
                        ),
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
                            new(
                                0,
                                EndHeight - Offset.Y,
                                (float)args.width,
                                EndHeight2 - Height2 - EndHeight
                            ),
                            0,
                            new(0),
                            color,
                            this
                        );
                    cl.DrawRect(
                        new(
                            s.X - Offset.X,
                            EndHeight - Height - Offset.Y,
                            (float)args.width - s.X + Offset.X,
                            Height
                        ),
                        0,
                        new(0),
                        color,
                        this
                    );
                    cl.DrawRect(
                        new(-Offset.X, EndHeight2 - Height2 - Offset.Y, s2.X + CursorWidth, Height),
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
