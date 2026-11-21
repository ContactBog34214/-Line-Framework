using System.Numerics;
using System.Runtime.InteropServices;
using Line.Framework.IO;
using Line.Framework.IO.Events;
using SDL3;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 原生事件 → 抽象层事件。
/// <para>
/// <b>必须在入队时调用</b>：文本等字段指向 SDL 内部缓冲区，下一次
/// <c>SDL_PumpEvents</c> 就覆写。
/// </para>
/// <para>
/// 成员名全部按 <c>SDL3-CS.xml</c>（NuGet 包内文档）核对过。
/// </para>
/// </summary>
internal static class SDL3EventConverter
{
    /// <summary>
    /// 转换。无法映射的类型返回 <see cref="UnknownEvent"/>（不丢弃）。
    /// </summary>
    public static IWindowEvent Convert(IWindow window, SDL.Event evt)
    {
        // SDL3 的时间戳在公共头上：Event.Common.Timestamp（毫秒，uint64）。
        // 不要写 evt.Timestamp —— Event 本身没有这个字段。
        var time = TimeSpan.FromMilliseconds(evt.Common.Timestamp);
        var type = (SDL.EventType)evt.Type;

        return type switch
        {
            SDL.EventType.KeyDown => new KeyboardEvent(
                window,
                time,
                (KeyCode)(uint)evt.Key.Key,
                isDown: true,
                isRepeat: evt.Key.Repeat,
                ConvertModifiers(evt.Key.Mod)
            ),

            SDL.EventType.KeyUp => new KeyboardEvent(
                window,
                time,
                (KeyCode)(uint)evt.Key.Key,
                isDown: false,
                isRepeat: false,
                ConvertModifiers(evt.Key.Mod)
            ),

            SDL.EventType.MouseMotion => new MouseMotionEvent(
                window,
                time,
                new Vector2(evt.Motion.X, evt.Motion.Y),
                new Vector2(evt.Motion.XRel, evt.Motion.YRel),
                ConvertButtonMask(evt.Motion.State)
            ),

            SDL.EventType.MouseButtonDown => new MouseButtonEvent(
                window,
                time,
                ToMouseButton(evt.Button.Button),
                isDown: true,
                new Vector2(evt.Button.X, evt.Button.Y),
                (byte)Math.Max(1, (int)evt.Button.Clicks)
            ),

            SDL.EventType.MouseButtonUp => new MouseButtonEvent(
                window,
                time,
                ToMouseButton(evt.Button.Button),
                isDown: false,
                new Vector2(evt.Button.X, evt.Button.Y),
                (byte)Math.Max(1, (int)evt.Button.Clicks)
            ),

            SDL.EventType.MouseWheel => new MouseWheelEvent(
                window,
                time,
                new Vector2(evt.Wheel.X, evt.Wheel.Y),
                evt.Wheel.Direction == SDL.MouseWheelDirection.Flipped,
                new Vector2(evt.Wheel.MouseX, evt.Wheel.MouseY)
            ),

            SDL.EventType.TextInput => new TextInputEvent(
                window,
                time,
                evt.Text.Text != nint.Zero ? Marshal.PtrToStringUTF8(evt.Text.Text) : string.Empty
            ),

            SDL.EventType.TextEditing => new TextEditingEvent(
                window,
                time,
                evt.Edit.Text != nint.Zero ? Marshal.PtrToStringUTF8(evt.Edit.Text) : string.Empty,
                evt.Edit.Start,
                evt.Edit.Length
            ),

            SDL.EventType.FingerDown => new TouchEvent(
                window,
                time,
                (long)evt.TFinger.FingerID,
                TouchPhase.Began,
                new Vector2(evt.TFinger.X, evt.TFinger.Y),
                new Vector2(evt.TFinger.DX, evt.TFinger.DY),
                evt.TFinger.Pressure,
                isPrimary: true
            ),

            SDL.EventType.FingerMotion => new TouchEvent(
                window,
                time,
                (long)evt.TFinger.FingerID,
                TouchPhase.Moved,
                new Vector2(evt.TFinger.X, evt.TFinger.Y),
                new Vector2(evt.TFinger.DX, evt.TFinger.DY),
                evt.TFinger.Pressure,
                isPrimary: true
            ),

            SDL.EventType.FingerUp => new TouchEvent(
                window,
                time,
                (long)evt.TFinger.FingerID,
                TouchPhase.Ended,
                new Vector2(evt.TFinger.X, evt.TFinger.Y),
                new Vector2(evt.TFinger.DX, evt.TFinger.DY),
                evt.TFinger.Pressure,
                isPrimary: true
            ),

            // SDL3 没有独立的「笔」事件类型（用的是 PProximity / PTouch / PMotion），
            // 成员尚未核对，先落到 UnknownEvent。

            SDL.EventType.WindowCloseRequested => new WindowCloseEvent(window, time),

            _ => new UnknownEvent(window, time, evt),
        };
    }

    /// <summary>
    /// SDL 修饰键 → 抽象层修饰键。
    /// <para>
    /// 注意 SDL3-CS 的命名是 <c>LGUI</c> / <c>RGUI</c>（全大写），不是 <c>LGui</c>。
    /// </para>
    /// </summary>
    private static KeyModifiers ConvertModifiers(SDL.Keymod mod)
    {
        var result = KeyModifiers.None;
        var m = (uint)mod;

        if ((m & (uint)SDL.Keymod.LShift) != 0)
            result |= KeyModifiers.LShift;
        if ((m & (uint)SDL.Keymod.RShift) != 0)
            result |= KeyModifiers.RShift;
        if ((m & (uint)SDL.Keymod.LCtrl) != 0)
            result |= KeyModifiers.LCtrl;
        if ((m & (uint)SDL.Keymod.RCtrl) != 0)
            result |= KeyModifiers.RCtrl;
        if ((m & (uint)SDL.Keymod.LAlt) != 0)
            result |= KeyModifiers.LAlt;
        if ((m & (uint)SDL.Keymod.RAlt) != 0)
            result |= KeyModifiers.RAlt;
        if ((m & (uint)SDL.Keymod.LGUI) != 0)
            result |= KeyModifiers.LGui;
        if ((m & (uint)SDL.Keymod.RGUI) != 0)
            result |= KeyModifiers.RGui;
        if ((m & (uint)SDL.Keymod.Num) != 0)
            result |= KeyModifiers.NumLock;
        if ((m & (uint)SDL.Keymod.Caps) != 0)
            result |= KeyModifiers.CapsLock;
        if ((m & (uint)SDL.Keymod.Mode) != 0)
            result |= KeyModifiers.Mode;

        return result;
    }

    /// <summary>
    /// SDL 鼠标 state 掩码 → 抽象层集合。
    /// <para>
    /// SDL3-CS 把 <c>SDL_BUTTON_MASK(X)</c> 暴露成<b>方法</b>
    /// <c>SDL.ButtonMask(int)</c>，参数是 1 起的按键号 —— 不是枚举成员。
    /// </para>
    /// </summary>
    private static MouseButtons ConvertButtonMask(SDL.MouseButtonFlags state)
    {
        var s = (uint)state;
        var result = MouseButtons.None;

        if ((s & (uint)SDL.ButtonMask(1)) != 0)
            result |= MouseButtons.Left;
        if ((s & (uint)SDL.ButtonMask(2)) != 0)
            result |= MouseButtons.Middle;
        if ((s & (uint)SDL.ButtonMask(3)) != 0)
            result |= MouseButtons.Right;
        if ((s & (uint)SDL.ButtonMask(4)) != 0)
            result |= MouseButtons.X1;
        if ((s & (uint)SDL.ButtonMask(5)) != 0)
            result |= MouseButtons.X2;

        return result;
    }

    /// <summary>
    /// SDL 的按键号（1 = 左，2 = 中，3 = 右，4/5 = 侧键）→ 抽象层。
    /// </summary>
    private static MouseButton ToMouseButton(byte button) =>
        button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            4 => MouseButton.X1,
            5 => MouseButton.X2,
            _ => MouseButton.Left, // 未知按键归到左键，避免抛异常打断事件流
        };
}
