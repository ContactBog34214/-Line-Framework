namespace Line.Framework.IO;

/// <summary>
/// 鼠标按键的<b>集合</b>（位标志）。
/// <para>
/// 与 <see cref="MouseButton"/> 分开是必要的：那个是「哪一个键」，
/// 取值 1~5，直接拿来位运算会冲突（Left=1 与 Middle=2 的按位或等于 Right=3）。
/// 本枚举按 <c>1 &lt;&lt; (n-1)</c> 编号，与 SDL 的 <c>state</c> 掩码一一对应。
/// </para>
/// </summary>
[Flags]
public enum MouseButtons
{
    None = 0,

    /// <summary>左键。</summary>
    Left = 1 << 0,

    /// <summary>中键。</summary>
    Middle = 1 << 1,

    /// <summary>右键。</summary>
    Right = 1 << 2,

    /// <summary>侧键 1。</summary>
    X1 = 1 << 3,

    /// <summary>侧键 2。</summary>
    X2 = 1 << 4,
}
