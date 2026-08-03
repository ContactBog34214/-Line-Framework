using System.Numerics;
using static SDL3.SDL;

namespace Line.Framework.IO;

public class InputManager
{
    private readonly Window _window;
    public Sdl3Mouse Mouse { get; } = new();
    public Sdl3Keyboard Keyboard { get; } = new();
    public Sdl3TouchDevice Touch { get; } = new();
    Vector2 LastMousePosition { get; set; } = new();

    // 事件
    public event Action<KeyCode> KeyDown;
    public event Action<KeyCode> KeyUp;
    public event Action<IMouse> MouseDown;
    public event Action<IMouse> MouseUp;
    public event Action<IMouse> MouseWheel; // 滚动增量（正值向下/右）
    public event Action<IMouse> MouseMove; // dx, dy 增量

    public string GetClipBoardText() => GetClipboardText();

    public InputManager(Window window)
    {
        _window = window;
        SubscribeEvents();
    }

    private void SubscribeEvents()
    {
        _window.EventPool.TryAdd(EventType.KeyDown, OnKeyDown);
        _window.EventPool.TryAdd(EventType.KeyUp, OnKeyUp);
        _window.EventPool.TryAdd(EventType.MouseButtonDown, OnMouseDown);
        _window.EventPool.TryAdd(EventType.MouseButtonUp, OnMouseUp);
        _window.EventPool.TryAdd(EventType.MouseWheel, OnMouseWheel);
        _window.EventPool.TryAdd(EventType.FingerDown, OnFingerDown);
        _window.EventPool.TryAdd(EventType.FingerUp, OnFingerUp);
        _window.EventPool.TryAdd(EventType.FingerMotion, OnFingerMove);
        _window.OnUpdate += (a, b) =>
        {
            OnMouseMove();
        };
    }

    private void OnKeyDown(Event evt)
    {
        var K = (KeyCode)evt.Key.Key;
        Keyboard.Keys.Add(K);
        KeyDown?.Invoke(K);
    }

    private void OnKeyUp(Event evt)
    {
        var K = (KeyCode)evt.Key.Key;
        Keyboard.Keys.Remove(K);
        KeyUp?.Invoke(K);
    }

    private void OnMouseDown(Event evt)
    {
        var bt = SDL3MB2LFMB((MouseButtonFlags)evt.Button.Button);
        Mouse.down.Add(bt);
        MouseDown?.Invoke(Mouse);
        CursorDown?.Invoke(Mouse);
    }

    private static MouseButton SDL3MB2LFMB(MouseButtonFlags mousebutton)
    {
        switch (mousebutton)
        {
            case MouseButtonFlags.Left:
                return MouseButton.Left;
            case MouseButtonFlags.Middle:
                return MouseButton.Middle;
            case MouseButtonFlags.Right:
                return MouseButton.Right;
            case MouseButtonFlags.X1:
                return MouseButton.X1;
            case MouseButtonFlags.X2:
                return MouseButton.X2;
            default:
                return MouseButton.Left;
        }
    }

    private void OnMouseUp(Event evt)
    {
        var bt = SDL3MB2LFMB((MouseButtonFlags)evt.Button.Button);
        Mouse.down.Remove(bt);
        MouseUp?.Invoke(Mouse);
        CursorUp?.Invoke(Mouse);
    }

    private void OnMouseWheel(Event evt)
    {
        Mouse.WheelDelta = new(evt.Wheel.X, evt.Wheel.Y);
        MouseWheel?.Invoke(Mouse);
    }

    private void OnMouseMove()
    {
        GetMouseState(out float x, out float y);
        float dx = x - LastMousePosition.X;
        float dy = y - LastMousePosition.Y;
        LastMousePosition = new(x, y);
        Mouse.Position = LastMousePosition * _window.Scale;
        if (dx != 0 && dy != 0)
        {
            MouseMove?.Invoke(Mouse);
            CursorMove?.Invoke(Mouse);
        }
    }

    // 状态查询
    public bool IsKeyDown(KeyCode key) => Keyboard.IsKeyDown(key);

    public bool IsMouseButtonDown(MouseButton button) => Mouse.IsMouseButtonDown(button);

    //触摸
    private void OnFingerDown(Event evt)
    {
        var id = evt.TFinger.FingerID;
        var position = new Vector2(evt.TFinger.X, evt.TFinger.Y);
        position *= _window.Size;
        var finger = new Sdl3TouchPoint() { Position = position * _window.Scale };
        Touch.Touches.TryAdd(id, finger);
        FingerDown?.Invoke((id, finger));
        CursorDown?.Invoke(finger);
    }

    public event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerDown;

    private void OnFingerUp(Event evt)
    {
        var id = evt.TFinger.FingerID;
        if (Touch.Touches.TryGetValue(id, out var touch))
        {
            Touch.Touches.Remove(id);
            var position = new Vector2(evt.TFinger.X, evt.TFinger.Y);
            position *= _window.Size;
            touch.Position = position * _window.Scale;
            FingerUp?.Invoke((id, (Sdl3TouchPoint)touch));
            CursorUp?.Invoke(touch);
        }
    }

    public event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerUp;

    private void OnFingerMove(Event evt)
    {
        var id = evt.TFinger.FingerID;
        if (Touch.Touches.TryGetValue(id, out var touch))
        {
            var position = new Vector2(evt.TFinger.X, evt.TFinger.Y);
            position *= _window.Size;
            touch.Position = position * _window.Scale;
            FingerMove?.Invoke((id, (Sdl3TouchPoint)touch));
            CursorMove?.Invoke(touch);
        }
    }

    public event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerMove;

    public event Action<ICursor> CursorDown;
    public event Action<ICursor> CursorMove;
    public event Action<ICursor> CursorUp;
}

public class Sdl3Mouse : IMouse
{
    public Vector2 Position { get; set; } = new();
    public Vector2 WheelDelta { get; set; } = new();
    internal List<MouseButton> down = [];

    public bool IsMouseButtonDown(MouseButton Button)
    {
        return down.Contains(Button);
    }
}

public class Sdl3Keyboard : IKey
{
    public List<KeyCode> Keys { get; set; } = [];

    public bool IsKeyDown(KeyCode key)
    {
        return Keys.Contains(key);
    }
}

public class Sdl3TouchDevice : ITouchDevice
{
    public Dictionary<ulong, ICursor> Touches { get; set; } = new();

    public ICursor GetTouch(ulong Id)
    {
        if (Touches.TryGetValue(Id, out var result))
            return result;
        return null;
    }
}

public class Sdl3TouchPoint : ICursor
{
    public Vector2 Position { get; set; } = new();
}
