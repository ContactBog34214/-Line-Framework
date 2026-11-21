using System.Numerics;

namespace Line.Framework.IO.Events;

/// <summary>触摸阶段。</summary>
public enum TouchPhase
{
    /// <summary>手指按下。</summary>
    Began,

    /// <summary>手指移动。</summary>
    Moved,

    /// <summary>手指抬起。</summary>
    Ended,

    /// <summary>触摸被取消（窗口失焦、系统手势等）。不会再有对应的 Ended。</summary>
    Canceled,
}

/// <summary>
/// 触摸事件。
/// <para>
/// <b>一个事件类型承载三个阶段</b>：三者字段完全相同，拆开只会让消费方
/// 写三份几乎一样的处理逻辑（还要处理「Canceled 之后又收到 Moved」这种非法序列）。
/// </para>
/// </summary>
public sealed class TouchEvent : IWindowEvent
{
    public TouchEvent(
        IWindow window,
        TimeSpan time,
        long fingerId,
        TouchPhase phase,
        Vector2 position,
        Vector2 delta,
        float pressure,
        bool isPrimary
    )
    {
        Window = window;
        Time = time;
        FingerId = fingerId;
        Phase = phase;
        Position = position;
        Delta = delta;
        Pressure = pressure;
        IsPrimary = isPrimary;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>手指标识。同一手指的 Began → Moved… → Ended 共用这个 ID。</summary>
    public long FingerId { get; }

    public TouchPhase Phase { get; }

    /// <summary>触摸点位置（逻辑坐标）。</summary>
    public Vector2 Position { get; }

    /// <summary>本次移动增量（逻辑坐标）。</summary>
    public Vector2 Delta { get; }

    /// <summary>归一化压力（0~1）。设备不支持时通常是 1。</summary>
    public float Pressure { get; }

    /// <summary>是否是「主」触点（多指时通常是最先按下的那根）。</summary>
    public bool IsPrimary { get; }
}

/// <summary>数位板 / 触控笔阶段。</summary>
public enum PenPhase
{
    /// <summary>笔尖接触。</summary>
    Down,

    /// <summary>笔尖移动（接触中）。</summary>
    Move,

    /// <summary>笔尖离开。</summary>
    Up,

    /// <summary>悬停移动（未接触，笔在感应范围内）。</summary>
    Hover,
}

/// <summary>
/// 数位板 / 触控笔事件。
/// <para>
/// 与 <see cref="TouchEvent"/> 分开：笔有 <see cref="Tilt"/> 与
/// <see cref="IsEraser"/>，手指没有；而手指有多指主次之分，笔通常单支。
/// </para>
/// </summary>
public sealed class PenEvent : IWindowEvent
{
    public PenEvent(
        IWindow window,
        TimeSpan time,
        long penId,
        PenPhase phase,
        Vector2 position,
        Vector2 delta,
        float pressure,
        Vector2 tilt,
        bool isEraser
    )
    {
        Window = window;
        Time = time;
        PenId = penId;
        Phase = phase;
        Position = position;
        Delta = delta;
        Pressure = pressure;
        Tilt = tilt;
        IsEraser = isEraser;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    public long PenId { get; }

    public PenPhase Phase { get; }

    /// <summary>笔尖位置（逻辑坐标）。</summary>
    public Vector2 Position { get; }

    /// <summary>本次移动增量（逻辑坐标）。</summary>
    public Vector2 Delta { get; }

    /// <summary>归一化压力（0~1）。</summary>
    public float Pressure { get; }

    /// <summary>笔倾角（-90~90 度，两轴）。</summary>
    public Vector2 Tilt { get; }

    /// <summary>是否是橡皮擦端。</summary>
    public bool IsEraser { get; }
}

/// <summary>
/// 文本输入（已提交的字符）。
/// <para>
/// <b>是 <see cref="string"/> 而不是指针或 byte[]</b>：文本在后端缓冲区里，
/// 下一次泵事件就会被覆写。必须由转换器在入队时解码成托管字符串。
/// </para>
/// </summary>
public sealed class TextInputEvent : IWindowEvent
{
    public TextInputEvent(IWindow window, TimeSpan time, string text)
    {
        Window = window;
        Time = time;
        Text = text ?? string.Empty;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>输入的字符（可能多于一个，例如粘贴或某些输入法一次提交多个）。</summary>
    public string Text { get; }
}

/// <summary>
/// 文本编辑（输入法组合过程中的中间态）。
/// <para>
/// <b>与 <see cref="TextInputEvent"/> 的区别</b>：这是「还在拼写」，
/// 会频繁触发且内容会变；<see cref="TextInputEvent"/> 是「已提交」。
/// 把编辑态当提交会导致拼音字母重复插入。
/// </para>
/// </summary>
public sealed class TextEditingEvent : IWindowEvent
{
    public TextEditingEvent(IWindow window, TimeSpan time, string text, int start, int length)
    {
        Window = window;
        Time = time;
        Text = text ?? string.Empty;
        Start = start;
        Length = length;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }

    /// <summary>当前的组合串（未提交）。空串表示组合被取消。</summary>
    public string Text { get; }

    /// <summary>光标在组合串中的位置。</summary>
    public int Start { get; }

    /// <summary>组合串中被选中的长度。</summary>
    public int Length { get; }
}

/// <summary>
/// 窗口关闭请求。
/// <para>
/// <b>为什么它进事件流</b>：与输入必须保持相对时序 ——
/// 「按 ESC 的同一帧要关窗」这种场景，走独立 event 会丢失顺序。
/// 其余窗口状态（尺寸、焦点…）留在 <see cref="IWindow"/> 的 event 上，
/// 因为它们是最新值语义，不需要排队。
/// </para>
/// </summary>
public sealed class WindowCloseEvent : IWindowEvent
{
    public WindowCloseEvent(IWindow window, TimeSpan time)
    {
        Window = window;
        Time = time;
    }

    public IWindow Window { get; }

    public TimeSpan Time { get; }
}
