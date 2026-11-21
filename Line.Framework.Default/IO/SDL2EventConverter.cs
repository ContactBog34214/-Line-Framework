using System.Numerics;
using System.Text;
using Line.Framework.IO;
using Line.Framework.IO.Events;
using S2 = SDL2.SDL;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL2 原生事件 → 抽象层事件。
/// <para>
/// <b>必须在入队时调用</b>：文本等字段指向 SDL 内部缓冲区，
/// 下一次 <c>SDL_PumpEvents</c> 就覆写。
/// </para>
/// <para>
/// 成员名按 <c>ppy.SDL2-CS 1.0.82</c> 实际的编译错误与 <c>strings</c> 输出核对：
/// 时间戳是 <c>evt.common.timestamp</c>（全小写）；修饰键常量在
/// <c>SDL_Keymod</c> 枚举里；窗口标志在 <c>SDL_WindowFlags</c> 枚举里。
/// </para>
/// </summary>
internal static class SDL2EventConverter
{
    /// <summary>
    /// 本地单调时钟，作为事件时间的临时来源。
    /// <para>
    /// 理想是用 SDL 事件自带的时间戳，但 <c>SDL2-CS</c> 的
    /// <c>SDL_Event</c> 上拿不到（见 <c>Convert</c> 里的 TODO）。
    /// 用本地时钟的后果是：时间反映的是「分发时刻」而不是「事件发生时刻」，
    /// 队列积压时两者会差出积压时长。对本框架的用途（计算帧间隔、
    /// 判定超时）够用。
    /// </para>
    /// </summary>
    private static readonly System.Diagnostics.Stopwatch Clock =
        System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// 转换。无法映射的类型返回 <see cref="UnknownEvent"/>（不丢弃）。
    /// <para>
    /// <c>unsafe</c>：文本事件里的 <c>text</c> 是定长缓冲，取址需要非安全上下文。
    /// </para>
    /// </summary>
    public static unsafe IWindowEvent Convert(IWindow window, S2.SDL_Event evt)
    {
        // TODO(sdl2): 用 SDL 的事件时间戳而不是本地时钟。
        // 目前拿不到：SDL2-CS 的 SDL_Event 上既没有 timestamp 也没有 common 字段，
        // union 成员命名不统一（只有 displayEvent / windowEvent 带 Event 后缀）。
        // 等确认字段名后改成读取原生时间戳。
        var time = Clock.Elapsed;
        var type = (S2.SDL_EventType)evt.type;

        return type switch
        {
            S2.SDL_EventType.SDL_KEYDOWN => new KeyboardEvent(
                window,
                time,
                (KeyCode)evt.key.keysym.sym,
                isDown: true,
                isRepeat: evt.key.repeat != 0,
                ConvertModifiers(evt.key.keysym.mod)
            ),

            S2.SDL_EventType.SDL_KEYUP => new KeyboardEvent(
                window,
                time,
                (KeyCode)evt.key.keysym.sym,
                isDown: false,
                isRepeat: false,
                ConvertModifiers(evt.key.keysym.mod)
            ),

            S2.SDL_EventType.SDL_MOUSEMOTION => new MouseMotionEvent(
                window,
                time,
                new Vector2(evt.motion.x, evt.motion.y),
                new Vector2(evt.motion.xrel, evt.motion.yrel),
                ConvertButtonMask(evt.motion.state)
            ),

            S2.SDL_EventType.SDL_MOUSEBUTTONDOWN => new MouseButtonEvent(
                window,
                time,
                ToMouseButton(evt.button.button),
                isDown: true,
                new Vector2(evt.button.x, evt.button.y),
                (byte)Math.Max(1, (int)evt.button.clicks)
            ),

            S2.SDL_EventType.SDL_MOUSEBUTTONUP => new MouseButtonEvent(
                window,
                time,
                ToMouseButton(evt.button.button),
                isDown: false,
                new Vector2(evt.button.x, evt.button.y),
                (byte)Math.Max(1, (int)evt.button.clicks)
            ),

            // SDL2 的滚轮事件在 2.0.14 之前没有 mouseX/mouseY 字段，
            // ppy 这个绑定里确实没有 —— 位置只能给零。
            S2.SDL_EventType.SDL_MOUSEWHEEL => new MouseWheelEvent(
                window,
                time,
                new Vector2(evt.wheel.x, evt.wheel.y),
                isFlipped: false,
                Vector2.Zero
            ),

            S2.SDL_EventType.SDL_TEXTINPUT => new TextInputEvent(
                window,
                time,
                ReadFixedText(evt.text.text)
            ),

            S2.SDL_EventType.SDL_TEXTEDITING => new TextEditingEvent(
                window,
                time,
                ReadFixedText(evt.edit.text),
                evt.edit.start,
                evt.edit.length
            ),

            S2.SDL_EventType.SDL_FINGERDOWN => new TouchEvent(
                window,
                time,
                evt.tfinger.fingerId,
                TouchPhase.Began,
                new Vector2(evt.tfinger.x, evt.tfinger.y),
                new Vector2(evt.tfinger.dx, evt.tfinger.dy),
                evt.tfinger.pressure,
                isPrimary: true
            ),

            S2.SDL_EventType.SDL_FINGERMOTION => new TouchEvent(
                window,
                time,
                evt.tfinger.fingerId,
                TouchPhase.Moved,
                new Vector2(evt.tfinger.x, evt.tfinger.y),
                new Vector2(evt.tfinger.dx, evt.tfinger.dy),
                evt.tfinger.pressure,
                isPrimary: true
            ),

            S2.SDL_EventType.SDL_FINGERUP => new TouchEvent(
                window,
                time,
                evt.tfinger.fingerId,
                TouchPhase.Ended,
                new Vector2(evt.tfinger.x, evt.tfinger.y),
                new Vector2(evt.tfinger.dx, evt.tfinger.dy),
                evt.tfinger.pressure,
                isPrimary: true
            ),

            // 关闭请求在 SDL2 里是 SDL_WINDOWEVENT 的子事件，不在顶层类型里。
            // 见 SDL2Window.TryHandleWindowEvent。

            _ => new UnknownEvent(window, time, evt),
        };
    }

    /// <summary>
    /// 读 SDL2 的定长 UTF-8 缓冲区（<c>fixed byte text[32]</c>）。
    /// <para>
    /// 是<b>固定大小缓冲</b>而不是 <c>byte[]</c>，取值需要 <c>unsafe</c> +
    /// 取首元素地址 —— 这也是为什么本方法标了 <c>unsafe</c>。
    /// </para>
    /// </summary>
    private static unsafe string ReadFixedText(byte* text)
    {
        if (text == null)
            return string.Empty;

        int len = 0;
        while (len < 32 && text[len] != 0)
            len++;

        return len == 0 ? string.Empty : Encoding.UTF8.GetString(text, len);
    }

    /// <summary>
    /// SDL2 修饰键 → 抽象层。常量都在 <c>SDL_Keymod</c> 枚举里。
    /// </summary>
    private static KeyModifiers ConvertModifiers(S2.SDL_Keymod mod)
    {
        var result = KeyModifiers.None;
        var m = (ushort)mod;

        if ((m & (ushort)S2.SDL_Keymod.KMOD_LSHIFT) != 0)
            result |= KeyModifiers.LShift;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_RSHIFT) != 0)
            result |= KeyModifiers.RShift;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_LCTRL) != 0)
            result |= KeyModifiers.LCtrl;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_RCTRL) != 0)
            result |= KeyModifiers.RCtrl;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_LALT) != 0)
            result |= KeyModifiers.LAlt;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_RALT) != 0)
            result |= KeyModifiers.RAlt;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_LGUI) != 0)
            result |= KeyModifiers.LGui;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_RGUI) != 0)
            result |= KeyModifiers.RGui;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_NUM) != 0)
            result |= KeyModifiers.NumLock;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_CAPS) != 0)
            result |= KeyModifiers.CapsLock;
        if ((m & (ushort)S2.SDL_Keymod.KMOD_MODE) != 0)
            result |= KeyModifiers.Mode;

        return result;
    }

    /// <summary>
    /// SDL2 鼠标 state 掩码 → 抽象层集合。
    /// <para>
    /// SDL2-CS 没有导出 <c>SDL_BUTTON_LMASK</c> 这类常量，
    /// 所以直接按位定义算：Button n 对应 <c>1 &lt;&lt; (n-1)</c>。
    /// </para>
    /// </summary>
    private static MouseButtons ConvertButtonMask(uint state)
    {
        var result = MouseButtons.None;

        if ((state & (1u << 0)) != 0)
            result |= MouseButtons.Left;
        if ((state & (1u << 1)) != 0)
            result |= MouseButtons.Middle;
        if ((state & (1u << 2)) != 0)
            result |= MouseButtons.Right;
        if ((state & (1u << 3)) != 0)
            result |= MouseButtons.X1;
        if ((state & (1u << 4)) != 0)
            result |= MouseButtons.X2;

        return result;
    }

    /// <summary>SDL2 的按键号（1 = 左，2 = 中，3 = 右，4/5 = 侧键）。</summary>
    private static MouseButton ToMouseButton(byte button) =>
        button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            4 => MouseButton.X1,
            5 => MouseButton.X2,
            _ => MouseButton.Left,
        };
}
