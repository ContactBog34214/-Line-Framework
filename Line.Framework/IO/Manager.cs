using System.Numerics;
using System.Runtime.InteropServices;
using static SDL3.SDL;
// SDL3 也在 SDL 类里定义了同名的事件结构（KeyboardEvent / MouseButtonEvent…）。
// `using static SDL3.SDL` 会把它们一并导入，与本项目的同名类型冲突（CS0104）。
// 逐个用别名指向本项目的事件类型，SDL 那边的则只用全名 SDL.Xxx。
using KeyboardEvent = Line.Framework.IO.Events.KeyboardEvent;
using MouseButtonEvent = Line.Framework.IO.Events.MouseButtonEvent;
using MouseMotionEvent = Line.Framework.IO.Events.MouseMotionEvent;
using MouseWheelEvent = Line.Framework.IO.Events.MouseWheelEvent;
using TextInputEvent = Line.Framework.IO.Events.TextInputEvent;
using TouchEvent = Line.Framework.IO.Events.TouchEvent;
using WindowCloseEvent = Line.Framework.IO.Events.WindowCloseEvent;
using TouchPhase = Line.Framework.IO.Events.TouchPhase;

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
        // 鼠标位置由 MouseMotionEvent 驱动（见 OnMotion）。
        //
        // 这里不再每帧轮询 GetMouseState —— 那是 SDL3 专用的 API，
        // 换成别的窗口实现后会返回恒 0。事件流对两个后端都成立。
        _window.OnEvent += HandleEvent;
    }

    /// <summary>
    /// 事件分发入口。
    /// <para>
    /// 按类型分派到各处理器。<b>不认识的类型直接忽略</b>（例如
    /// <see cref="UnknownEvent"/>）——输入层只关心自己那几类。
    /// </para>
    /// </summary>
    protected virtual Task HandleEvent(IWindowEvent evt)
    {
        switch (evt)
        {
            case KeyboardEvent k:
                return OnKey(k);
            case TextInputEvent t:
                return OnText(t);
            case MouseButtonEvent b:
                return OnMouseButton(b);
            case MouseWheelEvent w:
                return OnWheel(w);
            case MouseMotionEvent m:
                return OnMotion(m);
            case TouchEvent tc:
                return OnTouch(tc);
            case WindowCloseEvent:
                // 关闭请求由 WindowType.BindEvents 处理（它是「窗口」的事，不是输入）。
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    protected virtual Task OnKey(KeyboardEvent e)
    {
        if (e.IsDown)
        {
            // 重复事件不重复加列表，但照常通知 —— 消费方可能想要刷屏式的按键重复。
            if (!Keyboard.Keys.Contains(e.Key))
                Keyboard.Keys.Add(e.Key);

            KeyDown?.Invoke(e.Key);
        }
        else
        {
            Keyboard.Keys.Remove(e.Key);
            KeyUp?.Invoke(e.Key);
        }

        return Task.CompletedTask;
    }

    protected virtual Task OnText(TextInputEvent e)
    {
        if (!string.IsNullOrEmpty(e.Text))
            TextInput?.Invoke(e.Text);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 当输入文本时
    /// </summary>
    public virtual event Action<string> TextInput;

    protected virtual Task OnMouseButton(MouseButtonEvent e)
    {
        if (e.IsDown)
        {
            if (!Mouse.down.Contains(e.Button))
                Mouse.down.Add(e.Button);

            MouseDown?.Invoke(Mouse, e.Button);
            CursorDown?.Invoke(Mouse);
        }
        else
        {
            Mouse.down.Remove(e.Button);
            MouseUp?.Invoke(Mouse, e.Button);
            CursorUp?.Invoke(Mouse);
        }

        return Task.CompletedTask;
    }

    protected virtual Task OnWheel(MouseWheelEvent e)
    {
        Mouse.WheelDelta = e.Delta;
        MouseWheel?.Invoke(Mouse);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 鼠标移动。
    /// <para>
    /// <b>完全由事件驱动，不再轮询</b>。事件里的 <see cref="MouseMotionEvent.Position"/>
    /// 是逻辑坐标，这里只做 UI 缩放（<c>× Scale</c>）。
    /// </para>
    /// <para>
    /// <b>相对模式下用增量而非绝对位置</b>：光标被后端固定在原地，
    /// <see cref="MouseMotionEvent.Position"/> 不再变化，只有
    /// <see cref="MouseMotionEvent.Delta"/> 反映真实移动。累加后钳制在窗口内，
    /// 否则虚拟光标会一路飘出可视区。
    /// </para>
    /// <para>
    /// 之前这里依赖 <c>OnMouseMove</c> 轮询 <c>GetMouseState</c> —— 那是
    /// <b>SDL3 的 API</b>，换成 SDL2 窗口后返回恒为 0，表现为「自绘光标卡在左上角」。
    /// </para>
    /// </summary>
    protected virtual Task OnMotion(MouseMotionEvent e)
    {
        Vector2 next;

        if (_window.EnableMouseRelative)
        {
            // Delta 是逻辑坐标增量，同样乘 Scale 换算到 UI 空间。
            next = Mouse.Position + e.Delta * _window.Scale;

            // 钳制到窗口范围：相对模式没有天然边界，
            // 不钳制的话虚拟光标会随累计位移无限漂移。
            var bounds = _window.Size * _window.Scale;
            next = Vector2.Clamp(next, Vector2.Zero, bounds);
        }
        else
        {
            next = e.Position * _window.Scale;
        }

        if (next == Mouse.Position)
            return Task.CompletedTask;

        Mouse.Position = next;
        MouseMove?.Invoke(Mouse);
        CursorMove?.Invoke(Mouse);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 轮询式鼠标位置更新。
    /// <para>
    /// <b>基类已不再调用它</b>：位置改由 <see cref="OnMotion"/> 从事件里取。
    /// 保留为虚方法是因为 <c>VirtualIM</c>（UI 沙箱）重写了它做坐标变换。
    /// </para>
    /// <para>
    /// ⚠️ 这里的 <c>GetMouseState</c> 是 <b>SDL3 专用</b> API ——
    /// 窗口换成 SDL2 后会返回恒 0。这正是本次 SDL2 切换踩到的坑，
    /// 新代码不要依赖这个方法的输出。
    /// </para>
    /// </summary>
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
    protected virtual Task OnTouch(TouchEvent e)
    {
        // 事件里的坐标是逻辑坐标（0~Size），这里换算到 UI 空间。
        var position = e.Position * _window.Scale;

        // 事件用 long 表示手指 ID（后端可能是 32 位或 64 位），
        // 而现有触摸表用 ulong 做键 —— 转一次，负数不可能出现但也不该崩。
        var id = (ulong)e.FingerId;

        switch (e.Phase)
        {
            case TouchPhase.Began:
            {
                var finger = new Sdl3TouchPoint() { Position = position };
                Touch.Touches.TryAdd(id, finger);
                FingerDown?.Invoke((id, finger));
                CursorDown?.Invoke(finger);
                break;
            }

            case TouchPhase.Moved:
            {
                if (Touch.Touches.TryGetValue(id, out var touch) && touch is Sdl3TouchPoint t)
                {
                    t.Position = position;
                    FingerMove?.Invoke((id, t));
                    CursorMove?.Invoke(t);
                }
                break;
            }

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
            {
                if (Touch.Touches.TryGetValue(id, out var touch) && touch is Sdl3TouchPoint t)
                {
                    Touch.Touches.Remove(id);
                    t.Position = position;
                    FingerUp?.Invoke((id, t));
                    CursorUp?.Invoke(t);
                }
                break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 当手指按下时
    /// </summary>
    public virtual event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerDown;

    /// <summary>
    /// 当手指抬起时
    /// </summary>
    public virtual event Action<(ulong Id, Sdl3TouchPoint Finger)> FingerUp;

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
