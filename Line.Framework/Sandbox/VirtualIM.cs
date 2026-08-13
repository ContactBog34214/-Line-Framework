using System.Numerics;
using Line.Framework.IO;
using Line.Framework.UI;
using SharpGen.Runtime;

namespace Line.Framework.Sandbox;

/// <summary>
/// 沙盒虚拟io管理器
/// </summary>
public class VirtualIM : InputManager, IDisposable
{
    protected internal readonly UISandbox MainSandbox;
    protected internal InputManager im
    {
        get; set
        {
            if (value == field) return;
            field = value;
            SubscribeEvents();
        }
    }
    protected InputManager old_im;
    protected string TextClipBoardTemp = "";
    public override string GetClipBoardText()
    {
        if (MainSandbox.AllowClipboardPaste) TextClipBoardTemp = im?.GetClipBoardText() ?? "";
        else TextClipBoardTemp = "";
        return TextClipBoardTemp;
    }
    public override event Action<string> TextInput;
    protected virtual void OnTextInput(string s)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        if (!MainSandbox.AllowGetTextInputContext) return;
        TextInput?.Invoke(s);
    }
    protected Vector2 MousePositionTemp = new();
    public override event Action<ICursor> CursorMove;
    public override event Action<ICursor> CursorDown;
    public override event Action<ICursor> CursorUp;
    public override event Action<IMouse> MouseMove;
    protected virtual void OnMouseMove(IMouse cursor)
    {
        MouseMove?.Invoke(cursor);
        CursorMove?.Invoke(cursor);
    }
    protected override void OnMouseMove()
    {
        if (MainSandbox.AllowGetMousePosition)
            if (MainSandbox.AllowGboalInput || MainSandbox.Focus)
                MousePositionTemp = MainSandbox.MousePosition(im?.Mouse.Position ?? new());
        Mouse.Position = MousePositionTemp;
    }
    public override event Action<IMouse, MouseButton> MouseDown;
    protected virtual void OnMouseDown(IMouse mouse, MouseButton mb)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        var i = !(MainSandbox.MouseButtonBlackList?.Value ?? []).Contains(mb);
        if (MainSandbox.MouseButtonWhiteListMode) i = !i;
        if (!i) return;
        if (Mouse.down.Contains(mb)) return;
        Mouse.down.Add(mb);
        MouseDown?.Invoke(Mouse, mb);
        CursorDown?.Invoke(Mouse);
    }
    public override event Action<IMouse, MouseButton> MouseUp;
    protected virtual void OnMouseUp(IMouse mouse, MouseButton mb)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        var i = !(MainSandbox.MouseButtonBlackList?.Value ?? []).Contains(mb);
        if (MainSandbox.MouseButtonWhiteListMode) i = !i;
        if (!i) return;
        if (!Mouse.down.Contains(mb)) return;
        Mouse.down.Remove(mb);
        MouseUp?.Invoke(Mouse, mb);
        CursorUp?.Invoke(Mouse);
    }
    public override event Action<IMouse> MouseWheel;
    protected virtual void OnMouseWheel(IMouse mouse)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        if (!MainSandbox.AllowGetMouseWheel) return;
        Mouse.WheelDelta = mouse.WheelDelta;
        MouseWheel?.Invoke(Mouse);
    }
    public override event Action<KeyCode> KeyDown;
    public override event Action<KeyCode> KeyUp;
    protected virtual void OnKeyDown(KeyCode key)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        var i = !(MainSandbox.KeyBlackList?.Value ?? []).Contains(key);
        if (MainSandbox.KeyWhiteListMode) i = !i;
        if (!i) return;
        if (Keyboard.Keys.Contains(key)) return;
        Keyboard.Keys.Add(key);
        KeyDown?.Invoke(key);
    }
    protected virtual void OnKeyUp(KeyCode key)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        var i = !(MainSandbox.KeyBlackList?.Value ?? []).Contains(key);
        if (MainSandbox.KeyWhiteListMode) i = !i;
        if (!i) return;
        if (!Keyboard.Keys.Contains(key)) return;
        Keyboard.Keys.Remove(key);
        KeyUp?.Invoke(key);
    }
    public override event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerDown;
    protected virtual void OnFingerDown((ulong Id, Sdl3TouchPoint point) p)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        if (!MainSandbox.AllowTouch) return;
        if (Touch.Touches.TryGetValue(p.Id, out _)) return;
        Touch.Touches.TryAdd(p.Id, p.point);
        FingerDown?.Invoke(p);
    }
    public override event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerMove;
    protected virtual void OnFingerMove((ulong Id, Sdl3TouchPoint point) p)
    {
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        if (!MainSandbox.AllowTouch) return;
        if (!Touch.Touches.TryGetValue(p.Id, out var cs)) return;
        cs.Position = p.point.Position;
        FingerMove?.Invoke(p);
    }
    public override event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerUp;
    protected virtual void OnFingerUp((ulong Id, Sdl3TouchPoint point) p)
    {
        if (!Touch.Touches.TryGetValue(p.Id, out _)) return;
        Touch.Touches.Remove(p.Id);
        if ((!MainSandbox.AllowGboalInput) && !MainSandbox.Focus) return;
        if (!MainSandbox.AllowTouch) return;
        FingerUp?.Invoke(p);
    }
    public VirtualIM(UISandbox sandbox) : base(null)
    {
        MainSandbox = sandbox;
        InsideSandbox s = sandbox.Sandbox;
        s?.OnUpdate += (_) => OnMouseMove();
    }
    protected override void SubscribeEvents()
    {
        var om = old_im;
        om?.MouseMove -= OnMouseMove;
        om?.MouseDown -= OnMouseDown;
        om?.MouseUp -= OnMouseUp;
        om?.MouseWheel -= OnMouseWheel;
        om?.TextInput -= OnTextInput;
        om?.KeyDown -= OnKeyDown;
        om?.KeyUp -= OnKeyUp;
        om?.FingerDown -= OnFingerDown;
        om?.FingerMove -= OnFingerMove;
        om?.FingerUp -= OnFingerUp;
        old_im = im;
        im?.MouseMove += OnMouseMove;
        im?.MouseDown += OnMouseDown;
        im?.MouseUp += OnMouseUp;
        im?.MouseWheel += OnMouseWheel;
        im?.TextInput += OnTextInput;
        im?.KeyDown += OnKeyDown;
        im?.KeyUp += OnKeyUp;
        im?.FingerDown += OnFingerDown;
        im?.FingerMove += OnFingerMove;
        im?.FingerUp += OnFingerUp;
    }
    public virtual void Dispose()
    {
        im = null;
    }
}