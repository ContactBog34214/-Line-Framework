namespace Line.Framework.IO.Events;

/// <summary>
/// 无法映射的原始事件。
/// <para>
/// <b>兜底而不是丢弃</b>：后端的某些事件类型没有对应的预设（手柄、剪贴板、
/// 传感器、拖放…），把它们塞进这里交给消费方自己判断，比静默丢弃好 ——
/// 「事件不见了」是最难查的一类问题。
/// </para>
/// </summary>
public sealed class UnknownEvent : IWindowEvent
{
    public UnknownEvent(IWindow window, TimeSpan time, object native, bool isGlobal = false)
    {
        Window = window;
        Time = time;
        Native = native;
        IsGlobal = isGlobal;
    }

    /// <inheritdoc />
    public IWindow Window { get; }

    /// <inheritdoc />
    public TimeSpan Time { get; }

    /// <summary>
    /// 后端原生事件（<c>SDL3.SDL.Event</c> 或 <c>SDL2.SDL.SDL_Event</c> 的装箱）。
    /// <para>
    /// <b>不要直接依赖它的类型</b> —— 这是逃生口。要读具体字段请用
    /// <c>ISdl3Window.ToSdlEvent</c> / <c>ISdl2Window.ToSdl2Event</c>（后者目前在
    /// Default 项目里被暂时排除编译，见其 csproj 注释）。
    /// </para>
    /// </summary>
    public object Native { get; }

    /// <summary>
    /// 是否是「无来源窗口」的全局事件（被路由到 <see cref="Window"/> 代管）。
    /// </summary>
    public bool IsGlobal { get; }
}
