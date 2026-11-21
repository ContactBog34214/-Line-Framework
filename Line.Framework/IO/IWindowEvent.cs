namespace Line.Framework.IO;

/// <summary>
/// 一个窗口事件。
/// <para>
/// <b>只提供「谁、什么时候」</b>这两件所有事件共有的事；具体内容由子类型给
///（见 <c>Line.Framework.IO.Events</c> 命名空间里的预设：键盘 / 鼠标 /
/// 触摸 / 数位板 / 文本）。
/// </para>
/// <para>
/// <b>为什么是接口而不是 struct</b>：事件要在同一条队列里混装，
/// 接口才能做「取出后按类型判断」。代价是每个事件一次堆分配 ——
/// 鼠标 1000Hz 下约 50KB/s 的 Gen0 垃圾，可以接受。
/// </para>
/// <para>
/// <b>坐标系</b>：所有位置 / 位移坐标都是<b>逻辑坐标</b>，
/// 范围 <c>[0,0] ~ [IWindow.Size.X, IWindow.Size.Y]</c>。
/// 不做任何 UI 缩放 —— 那是消费方的策略，事件只报告原始事实。
/// </para>
/// </summary>
public interface IWindowEvent
{
    /// <summary>
    /// 产生该事件的窗口。
    /// <para>
    /// 全局事件（设备热插拔、剪贴板等）本身没有来源窗口，会被路由给某个窗口
    /// 代管，这时指向代管窗口 —— 见 <see cref="UnknownEvent.IsGlobal"/>。
    /// </para>
    /// </summary>
    IWindow Window { get; }

    /// <summary>
    /// 事件发生时间。
    /// <para>
    /// <b>来源是后端的单调时钟</b>（SDL 的 <c>timestamp</c>，毫秒），
    /// 起点是后端初始化时刻而不是进程启动，因此只能算差值，不能当墙钟。
    /// </para>
    /// </summary>
    TimeSpan Time { get; }
}
