using System.Numerics;

namespace Line.Framework.IO.Events;

/// <summary>
/// 键盘按下 / 松开。
/// <para>
/// <b>不含字符</b>：字符走 <see cref="TextInputEvent"/>。
/// 按键事件回答「物理上按了哪个键」，文本事件回答「输入了什么字符」——
/// 中文/日文输入法下两者完全对不上（一个按键可能产生多个字符，
/// 也可能一个字符都没产生）。
/// </para>
/// </summary>
public sealed class KeyboardEvent : IWindowEvent
{
    public KeyboardEvent(
        IWindow window,
        TimeSpan time,
        KeyCode key,
        bool isDown,
        bool isRepeat,
        KeyModifiers modifiers
    )
    {
        Window = window;
        Time = time;
        Key = key;
        IsDown = isDown;
        IsRepeat = isRepeat;
        Modifiers = modifiers;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>按键（键盘布局相关，后端 keycode）。</summary>
    public KeyCode Key { get; }

    /// <summary>true = 按下，false = 松开。</summary>
    public bool IsDown { get; }

    /// <summary>是否是长按产生的重复事件。</summary>
    public bool IsRepeat { get; }

    /// <summary>此刻的修饰键状态。</summary>
    public KeyModifiers Modifiers { get; }
}

/// <summary>
/// 鼠标移动。
/// <para>
/// 坐标是<b>逻辑坐标</b>（<c>[0,0] ~ Size</c>）。相对模式下
/// <see cref="Position"/> 不再有意义（光标被固定），应改用 <see cref="Delta"/>。
/// </para>
/// </summary>
public sealed class MouseMotionEvent : IWindowEvent
{
    public MouseMotionEvent(
        IWindow window,
        TimeSpan time,
        Vector2 position,
        Vector2 delta,
        MouseButtons buttons
    )
    {
        Window = window;
        Time = time;
        Position = position;
        Delta = delta;
        Buttons = buttons;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>光标位置（逻辑坐标）。</summary>
    public Vector2 Position { get; }

    /// <summary>本次移动的增量（逻辑坐标）。相对模式下这是唯一可靠的数据。</summary>
    public Vector2 Delta { get; }

    /// <summary>此刻按下的鼠标键集合。</summary>
    public MouseButtons Buttons { get; }
}

/// <summary>鼠标按键按下 / 松开。</summary>
public sealed class MouseButtonEvent : IWindowEvent
{
    public MouseButtonEvent(
        IWindow window,
        TimeSpan time,
        MouseButton button,
        bool isDown,
        Vector2 position,
        byte clickCount
    )
    {
        Window = window;
        Time = time;
        Button = button;
        IsDown = isDown;
        Position = position;
        ClickCount = clickCount;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    public MouseButton Button { get; }

    public bool IsDown { get; }

    /// <summary>按下时鼠标在哪（逻辑坐标）。</summary>
    public Vector2 Position { get; }

    /// <summary>连击计数（1 = 单击，2 = 双击）。后端不支持时为 1。</summary>
    public byte ClickCount { get; }
}

/// <summary>鼠标滚轮。</summary>
public sealed class MouseWheelEvent : IWindowEvent
{
    public MouseWheelEvent(
        IWindow window,
        TimeSpan time,
        Vector2 delta,
        bool isFlipped,
        Vector2 mousePosition
    )
    {
        Window = window;
        Time = time;
        Delta = delta;
        IsFlipped = isFlipped;
        MousePosition = mousePosition;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>
    /// 滚动量。<b>正值方向随后端 / 平台而变</b>，用 <see cref="IsFlipped"/>
    /// 判断是否被系统反向。
    /// </summary>
    public Vector2 Delta { get; }

    /// <summary>系统是否反转了滚动方向（触控板「自然滚动」）。</summary>
    public bool IsFlipped { get; }

    /// <summary>滚动时光标位置（逻辑坐标）。</summary>
    public Vector2 MousePosition { get; }
}
