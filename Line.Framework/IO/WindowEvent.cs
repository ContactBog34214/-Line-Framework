namespace Line.Framework.IO;

/// <summary>
/// 一个窗口事件。
/// <para>
/// <b>过渡期的极简形态</b>：抽象层还没有自己的事件 union，因此这里只携带
/// 「属于谁」和一个不透明的后端事件。要读具体字段，需要用后端能力接口
/// （如 <c>ISdl3Window.ToSdlEvent</c>）转回去。
/// </para>
/// <para>
/// 等出现第二个后端时再决定要不要把它长成完整的 union（键盘 / 鼠标 / 触摸 / 文本…）。
/// 现在就定义字段等于在没有对照的情况下猜，猜错的代价很高。
/// </para>
/// </summary>
public readonly struct WindowEvent
{
    public WindowEvent(
        uint windowId,
        object payload,
        bool isGlobal = false,
        string text = null
    )
    {
        WindowId = windowId;
        Payload = payload;
        IsGlobal = isGlobal;
        Text = text;
    }

    /// <summary>
    /// 归属窗口的 <see cref="IWindow.Id"/>。
    /// <para>
    /// 全局事件（设备热插拔、剪贴板等）本身没有归属窗口，这里填的是「代管它的窗口」。
    /// 想区分两者请看 <see cref="IsGlobal"/>。
    /// </para>
    /// </summary>
    public uint WindowId { get; }

    /// <summary>是否是全局事件（原本无归属窗口，被路由到了某个窗口）。</summary>
    public bool IsGlobal { get; }

    /// <summary>
    /// 文本内容（文本输入 / 文本编辑 / 拖放文本）。
    /// <para>
    /// <b>必须在入队时就复制出来</b>：原生事件里的字符串是后端内部队列内存，
    /// 下一次泵事件就会被覆写。用 <c>GCHandle</c> 固定也无济于事 ——
    /// 那块内存由后端分配，不是 GC 管的托管对象。
    /// </para>
    /// <para>
    /// 因此 <see cref="Payload"/> 里的指针字段<b>不可再使用</b>，
    /// 需要文本一律读这个属性。
    /// </para>
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 后端原生事件（现阶段是 <c>SDL.Event</c> 的装箱）。
    /// <para>
    /// <b>不要直接依赖它的类型</b> —— 这是过渡期的逃生口，抽象层定型后会移除。
    /// 需要访问时走后端的能力接口做显式转换。
    /// </para>
    /// <para>
    /// <b>其中指向后端内存的指针字段已失效</b>（见 <see cref="Text"/>）。
    /// </para>
    /// </summary>
    public object Payload { get; }
}
