namespace Line.Framework.IO;

/// <summary>
/// 键盘修饰键状态。
/// <para>
/// 与 <see cref="KeyCode"/> 分开：<see cref="KeyCode"/> 是「按了哪个键」，
/// 这里是「此刻哪些修饰键是按下状态」，两者在内核里本来就是不同来源
/// （keycode vs modifier state）。
/// </para>
/// <para>
/// 左右分开是必要的：Shift+Enter 与 Ctrl+Enter 在很多软件里语义不同，
/// 而左/右 Shift 在输入法里也有区别。需要「任意 Shift」时用位或判断。
/// </para>
/// </summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>无修饰键。</summary>
    None = 0,

    /// <summary>左 Shift。</summary>
    LShift = 1 << 0,

    /// <summary>右 Shift。</summary>
    RShift = 1 << 1,

    /// <summary>左 Ctrl。</summary>
    LCtrl = 1 << 2,

    /// <summary>右 Ctrl。</summary>
    RCtrl = 1 << 3,

    /// <summary>左 Alt。</summary>
    LAlt = 1 << 4,

    /// <summary>右 Alt（AltGr）。</summary>
    RAlt = 1 << 5,

    /// <summary>左 GUI 键（Win / Super / Cmd）。</summary>
    LGui = 1 << 6,

    /// <summary>右 GUI 键。</summary>
    RGui = 1 << 7,

    /// <summary>Num Lock 激活。</summary>
    NumLock = 1 << 8,

    /// <summary>Caps Lock 激活（不是「按了 Caps Lock」，是「此刻锁定中」）。</summary>
    CapsLock = 1 << 9,

    /// <summary>AltGr / Mode 激活。</summary>
    Mode = 1 << 10,

    /// <summary>任意 Shift。</summary>
    Shift = LShift | RShift,

    /// <summary>任意 Ctrl。</summary>
    Ctrl = LCtrl | RCtrl,

    /// <summary>任意 Alt。</summary>
    Alt = LAlt | RAlt,

    /// <summary>任意 GUI 键。</summary>
    Gui = LGui | RGui,
}
