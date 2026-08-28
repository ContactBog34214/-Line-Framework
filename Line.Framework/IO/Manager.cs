using System.Numerics;
using System.Runtime.InteropServices;
using static SDL3.SDL;

namespace Line.Framework.IO;

/// <summary>
/// 输入管理器
/// </summary>
public class InputManager
{
    protected readonly WindowType _window;

    /// <summary>
    /// 鼠标对象
    /// </summary>
    public virtual Sdl3Mouse Mouse { get; } = new();

    /// <summary>
    /// 键盘对象
    /// </summary>
    public virtual Sdl3Keyboard Keyboard { get; } = new();

    /// <summary>
    /// 触摸对象
    /// </summary>
    public virtual Sdl3TouchDevice Touch { get; } = new();
    protected virtual Vector2 LastMousePosition { get; set; } = new();

    // 事件
    /// <summary>
    /// 当按键按下时
    /// </summary>
    public virtual event Action<KeyCode> KeyDown;

    /// <summary>
    /// 当按键松开时
    /// </summary>
    public virtual event Action<KeyCode> KeyUp;

    /// <summary>
    /// 当鼠标按下时
    /// </summary>
    public virtual event Action<IMouse, MouseButton> MouseDown;

    /// <summary>
    /// 当鼠标松开时
    /// </summary>
    public virtual event Action<IMouse, MouseButton> MouseUp;

    /// <summary>
    /// 当鼠标滚轮滚动时
    /// </summary>
    public virtual event Action<IMouse> MouseWheel; // 滚动增量（正值向下/右）

    /// <summary>
    /// 当鼠标移动时
    /// </summary>
    public virtual event Action<IMouse> MouseMove; // dx, dy 增量

    /// <summary>
    /// 获取剪切板文本
    /// </summary>
    /// <returns>系统剪切板文本</returns>
    public virtual string GetClipBoardText() => GetClipboardText();
    /// <summary>
    /// 最新一个光标位置
    /// </summary>
    public Vector2 CursorPosition { get; set; }

    public InputManager(WindowType window)
    {
        _window = window;
        SubscribeEvents();
        CursorMove += (i) => CursorPosition = i.Position;
    }

    protected virtual void SubscribeEvents()
    {
        _window.EventPool.TryAdd(EventType.KeyDown, OnKeyDown);
        _window.EventPool.TryAdd(EventType.KeyUp, OnKeyUp);
        _window.EventPool.TryAdd(EventType.MouseButtonDown, OnMouseDown);
        _window.EventPool.TryAdd(EventType.MouseButtonUp, OnMouseUp);
        _window.EventPool.TryAdd(EventType.MouseWheel, OnMouseWheel);
        _window.EventPool.TryAdd(EventType.FingerDown, OnFingerDown);
        _window.EventPool.TryAdd(EventType.FingerUp, OnFingerUp);
        _window.EventPool.TryAdd(EventType.FingerMotion, OnFingerMove);
        _window.EventPool.TryAdd(EventType.TextInput, OnTextInput);
        _window.OnUpdate += (_) =>
        {
            OnMouseMove();
        };
    }

    protected virtual async Task OnKeyDown(Event evt,object[] _)
    {
        var K = (KeyCode)evt.Key.Key;
        Keyboard.Keys.Add(K);
        KeyDown?.Invoke(K);
    }

    protected virtual async Task OnTextInput(Event evt,object[] Extra)
    {
        TextInput?.Invoke(Extra[0].ToString());
    }

    /// <summary>
    /// 当输入文本时
    /// </summary>
    public virtual event Action<string> TextInput;

    protected virtual async Task OnKeyUp(Event evt,object[] _)
    {
        var K = (KeyCode)evt.Key.Key;
        Keyboard.Keys.Remove(K);
        KeyUp?.Invoke(K);
    }

    protected virtual async Task OnMouseDown(Event evt,object[] _)
    {
        var bt = SDL3MB2LFMB((MouseButtonFlags)evt.Button.Button);
        Mouse.down.Add(bt);
        MouseDown?.Invoke(Mouse, bt);
        CursorDown?.Invoke(Mouse);
    }

    protected static MouseButton SDL3MB2LFMB(MouseButtonFlags mousebutton)
    {
        return (MouseButton)mousebutton;
    }

    protected virtual async Task OnMouseUp(Event evt,object[] _)
    {
        var bt = SDL3MB2LFMB((MouseButtonFlags)evt.Button.Button);
        Mouse.down.Remove(bt);
        MouseUp?.Invoke(Mouse, bt);
        CursorUp?.Invoke(Mouse);
    }

    protected virtual async Task OnMouseWheel(Event evt,object[] _)
    {
        Mouse.WheelDelta = new(evt.Wheel.X, evt.Wheel.Y);
        MouseWheel?.Invoke(Mouse);
    }

    protected virtual void OnMouseMove()
    {
        GetMouseState(out float x, out float y);
        float dx = x - LastMousePosition.X;
        float dy = y - LastMousePosition.Y;
        LastMousePosition = new(x, y);
        Mouse.Position = LastMousePosition * _window.Scale;
        if (dx != 0 || dy != 0)
        {
            MouseMove?.Invoke(Mouse);
            CursorMove?.Invoke(Mouse);
        }
    }

    // 状态查询
    /// <summary>
    /// 判断按键是否按下
    /// </summary>
    /// <param name="按键"></param>
    /// <returns>按下状态</returns>
    public virtual bool IsKeyDown(KeyCode key) => Keyboard.IsKeyDown(key);

    /// <summary>
    /// 判断鼠标按键是否按下
    /// </summary>
    /// <param name="鼠标按键"></param>
    /// <returns>按下状态</returns>
    public virtual bool IsMouseButtonDown(MouseButton button) => Mouse.IsMouseButtonDown(button);

    //触摸
    protected virtual async Task OnFingerDown(Event evt,object[] _)
    {
        var id = evt.TFinger.FingerID;
        var position = new Vector2(evt.TFinger.X, evt.TFinger.Y);
        position *= _window.Size;
        var finger = new Sdl3TouchPoint() { Position = position * _window.Scale };
        Touch.Touches.TryAdd(id, finger);
        FingerDown?.Invoke((id, finger));
        CursorDown?.Invoke(finger);
    }

    /// <summary>
    /// 当手指按下时
    /// </summary>
    public virtual event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerDown;

    protected virtual async Task OnFingerUp(Event evt,object[] _)
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

    /// <summary>
    /// 当手指抬起时
    /// </summary>
    public virtual event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerUp;

    protected virtual async Task OnFingerMove(Event evt,object[] _)
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

    /// <summary>
    /// 当手指移动时
    /// </summary>
    public virtual event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerMove;

    /// <summary>
    /// 当指针设备按下时
    /// </summary>
    public virtual event Action<ICursor> CursorDown;

    /// <summary>
    /// 当指针设备移动时
    /// </summary>
    public virtual event Action<ICursor> CursorMove;

    /// <summary>
    /// 当指针设备松开时
    /// </summary>
    public virtual event Action<ICursor> CursorUp;
}

public class Sdl3Mouse : IMouse
{
    /// <summary>
    /// 光标绝对位置
    /// </summary>
    public Vector2 Position { get; set; } = new();

    /// <summary>
    /// 滚轮增量
    /// </summary>
    public Vector2 WheelDelta { get; set; } = new();
    internal List<MouseButton> down = [];

    /// <summary>
    /// 鼠标按键是否被按下
    /// </summary>
    /// <param name="鼠标键"></param>
    /// <returns>按下状态</returns>
    public bool IsMouseButtonDown(MouseButton Button)
    {
        return down.Contains(Button);
    }
}

public class Sdl3Keyboard : IKey
{
    /// <summary>
    /// 被按下的按键
    /// </summary>
    public List<KeyCode> Keys { get; set; } = [];

    /// <summary>
    /// 按键是否被按下
    /// </summary>
    /// <param name="按键"></param>
    /// <returns>按下状态</returns>
    public bool IsKeyDown(KeyCode key)
    {
        return Keys.Contains(key);
    }
}

public class Sdl3TouchDevice : ITouchDevice
{
    /// <summary>
    /// 所有触摸点
    /// </summary>
    public Dictionary<ulong, ICursor> Touches { get; set; } = new();

    /// <summary>
    /// 获取触摸点
    /// </summary>
    /// <param name="触摸点ID"></param>
    /// <returns>触摸点对象</returns>
    public ICursor GetTouch(ulong Id)
    {
        if (Touches.TryGetValue(Id, out var result))
            return result;
        return null;
    }
}

public class Sdl3TouchPoint : ICursor
{
    /// <summary>
    /// 触摸点绝对位置
    /// </summary>
    public Vector2 Position { get; set; } = new();
}
