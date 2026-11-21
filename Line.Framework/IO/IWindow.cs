using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Types;

namespace Line.Framework.IO;

/// <summary>
/// 一个窗口对象。
/// <para>
/// <b>它只是窗口</b>：没有游戏循环、没有图形设备、没有交换链、没有 UI 根节点、
/// 没有资源管理器、没有事件池。把这些装配起来是
/// <c>Line.Framework.Default.App</c> 的职责 —— App 只是官方提供的便利整合器，
/// 想自己组织的调用方可以完全绕开它，直接用本接口 + <see cref="IGraphicsDevice"/>。
/// </para>
/// <para>
/// <b>唯一的图形出口是 <see cref="Source"/></b>：窗口把所有已知的原生句柄一次填满，
/// 各后端只读自己那份、读不到就抛。<b>窗口不知道后端是谁</b> ——
/// 这是「原生指针语义错配」那类事故的结构性防线
/// （历史事故：Wayland 的 <c>wl_surface*</c> 被当作 <c>SDL_Window*</c> 传给 SDL GPU，
/// 报出的却是「不支持透明窗口」，完全误导排查方向）。
/// </para>
/// <para>
/// <b>刻意不包含的成员</b>（各自的归属）：
/// <c>Scale</c> → App；<c>SyncToVerticalBlank</c> → <see cref="ISwapchain"/>；
/// <c>ShowCursor</c> / <c>RelativeMouse</c> / <c>MouseSpeedScale</c> / <c>TextInput</c>
/// → <see cref="Attributes"/>。这条分界要守住，否则本接口会重新长出
/// <c>WindowType</c> 那种杂糅。
/// </para>
/// </summary>
public interface IWindow : IDisposable, IName
{
    /// <summary>
    /// 原生窗口来源。
    /// <para>
    /// 按当前平台 / 视频驱动填满能填的字段。后端拿不到自己要的那份时
    /// <b>必须抛</b>，不允许「用别的字段兜底猜一下」。
    /// </para>
    /// </summary>
    SwapchainSource Source { get; }

    /// <summary>
    /// 平台窗口 ID（<c>SDL_WindowID</c> 等），用于把事件分派给正确的窗口。
    /// </summary>
    uint Id { get; }

    /// <summary>
    /// 逻辑尺寸。用于 UI 布局。
    /// <para><b>绝不能</b>用它创建交换链</para>
    /// </summary>
    Vector2 Size { get; set; }

    /// <summary>
    /// 像素尺寸。
    /// <para>
    /// <b>优先返回真实像素尺寸</b>，这是创建 / 重建交换链应当使用的尺寸 ——
    /// HiDPI / 分数缩放下它大于 <see cref="Size"/>，用错会让 present 静默失败。
    /// </para>
    /// <para>
    /// <b>但它可能是近似值</b>：部分后端在没有渲染上下文时拿不到真实像素尺寸
    ///（例如 SDL2 需要先有 GL 或 Vulkan 上下文），此时实现会按 DPI 反推，
    /// 反推不了就退回 <see cref="Size"/>（等价于 DPI 缩放视作 1）。
    /// 实现应在首次退化时打一条日志。
    /// </para>
    /// <para>只读 —— 像素尺寸由 DPI 与窗口管理器决定，开放 setter 只会制造谎言。</para>
    /// </summary>
    Vector2 PixelSize { get; }

    /// <summary>窗口位置（逻辑坐标，屏幕左上为原点）。</summary>
    Vector2 Position { get; set; }

    /// <summary>标题。</summary>
    string Title { get; set; }

    /// <summary>窗口是否仍然存在。</summary>
    bool Exists { get; }

    /// <summary>是否持有键盘焦点。</summary>
    bool IsFocus { get; }

    /// <summary>
    /// 鼠标是否落在本窗口上。
    /// <para>
    /// 与 <see cref="IsFocus"/> 是两回事：键盘焦点可以不在本窗口，
    /// 而鼠标仍在上面悬停。旧实现把它写成
    /// <c>SDL.GetMouseFocus() == WindowHandle</c>，句柄不该出现在 core 的契约里。
    /// </para>
    /// </summary>
    bool IsMouseFocus { get; }

    /// <summary>是否可见。</summary>
    bool Visible { get; set; }

    /// <summary>
    /// 是否全屏。
    /// <para>
    /// 它是窗口的<b>形态</b>而不是开关，所以留在强类型属性里，
    /// 不进 <see cref="Attributes"/>（将来退出全屏还要恢复原尺寸）。
    /// </para>
    /// <para>
    /// <b>只表达「是否全屏」，不区分独占 / 无边框</b> —— SDL2 的
    /// <c>FULLSCREEN</c> 与 <c>FULLSCREEN_DESKTOP</c> 在这里压扁成一个 bool，
    /// 由实现选一种。需要独占全屏时要把它扩展成枚举。
    /// </para>
    /// </summary>
    bool FullScreen { get; set; }

    /// <summary>
    /// 窗口级可选属性（光标、相对鼠标、文本输入、置顶、透明度…）。
    /// 见 <see cref="WindowAttributes"/> 的线程模型说明。
    /// </summary>
    WindowAttributes Attributes { get; }

    /// <summary>
    /// 请求关闭。
    /// <para>
    /// <b>只发通知</b>：不自己释放任何东西。由持有方（App 或你）决定善后 ——
    /// 是弹确认框、还是直接销毁。旧实现把「点 X」和「Dispose 自己」揉在
    /// 同一个回调里，导致两者无法区分。
    /// </para>
    /// </summary>
    void RequestClose();

    /// <summary>
    /// 让本窗口的事件变得可读。
    /// <para>
    /// 语义是「把属于我的事件准备好」，实现方式由后端决定：
    /// 真正每窗口队列的系统可以直接泵自己；SDL3 这类进程级队列的系统
    /// 泵一次全局再按窗口 ID 分发。
    /// </para>
    /// <para>
    /// <b>必须在主线程调用</b>：SDL 的事件泵在 Windows 上要求主线程，
    /// 且实现不得与「等待事件」类 API（<c>SDL_WaitEvent</c>）混用。
    /// </para>
    /// </summary>
    void PumpEvents();

    /// <summary>
    /// 取走第一个匹配的事件。
    /// <para>
    /// <b>不匹配的事件不会丢失，也不会乱序</b> —— 它们留在队列里，
    /// 等下一次用别的 filter 来取。因此这是「按需筛选」而不是「过滤后丢弃」。
    /// </para>
    /// <para>
    /// 顺序保证：更早到达的事件一定更早被返回。
    /// </para>
    /// </summary>
    /// <param name="evt">取到的事件。</param>
    /// <param name="filter">
    /// 筛选条件；<c>null</c> 表示取最旧的一个。
    /// </param>
    /// <returns>取到返回 true；队列里没有匹配的事件返回 false。</returns>
    bool PollEvent(out IWindowEvent evt, Func<IWindowEvent, bool> filter = null);

    /// <summary>
    /// 提升到前台并请求焦点。
    /// <para>
    /// 对应旧实现里的 <c>SDL_RaiseWindow</c> + <c>SDL_ShowWindow</c> 组合。
    /// 窗口管理器可以拒绝，因此它只是「请求」。
    /// </para>
    /// </summary>
    void Raise();

    /// <summary>
    /// 从流设置窗口图标。
    /// <para>
    /// <b>流格式与 <c>RResourceSet</c> 同款（LTX v1）</b>：
    /// <c>ushort</c> 小端元数据长度 + <c>TextureResourceStreamInfo</c>
    ///（宽 / 高 / 格式 / 层数）+ 紧凑像素数据。调用方负责解码图片，
    /// 窗口只负责把像素交给系统 —— <b>图像解码不进核心库</b>。
    /// </para>
    /// <para>
    /// <b>为什么解码放在调用方</b>：SDL3 有内置的图像加载（<c>SDL_LoadSurfaceIO</c>），
    /// 但 SDL2 没有（需要额外的 SDL_image）。把解码留在调用方，接口在
    /// 两个后端上才是一致的 —— 否则 <see cref="SetIcon"/> 会变成「只有某些后端能用」。
    /// </para>
    /// </summary>
    /// <param name="iconStream">LTX v1 格式的图标数据。</param>
    /// <param name="closeStream">处理完后是否关闭流。</param>
    void SetIcon(Stream iconStream, bool closeStream = false);

    /// <summary>
    /// 立即抓取 / 释放鼠标（不改 <see cref="WindowAttributes.RelativeMouse"/>）。
    /// <para>
    /// 为什么单独一个方法：<see cref="WindowAttributes.RelativeMouse"/> 是
    /// <b>用户意图</b>，而这个方法是<b>临时状态</b> —— 例如鼠标贴到窗口边缘时
    /// 临时释放抓取，让用户能把光标移出窗口。
    /// </para>
    /// <para>
    /// <b>多窗口语义：此刻抓取鼠标</b>。同一时刻只有一个窗口拥有抓取 ——
    /// 焦点在谁身上谁说了算。SDL3 有窗口级的相对模式，SDL2 只有进程级的
    /// <c>SDL_SetRelativeMouseMode</c>，因此 SDL2 的实现会在<b>非焦点时不做任何事</b>，
    /// 避免影响真正持有焦点的那个窗口。
    /// </para>
    /// </summary>
    void ApplyMouseCapture(bool capture);

    /// <summary>
    /// 尺寸变化。
    /// <para>
    /// <b>无参</b>：处理器自己读 <see cref="Size"/> / <see cref="PixelSize"/>。
    /// 带参数会让「事件里的值」与「属性里的值」成为两套真相，HiDPI 下必然错位。
    /// </para>
    /// </summary>
    event Action Resized;

    /// <summary>位置变化。无参，理由同 <see cref="Resized"/>。</summary>
    event Action Moved;

    /// <summary>获得焦点。</summary>
    event Action FocusGained;

    /// <summary>失去焦点。</summary>
    event Action FocusLost;

    /// <summary>
    /// 有人要关这个窗口（用户点 X / 系统请求 / <see cref="RequestClose"/>）。
    /// 与 <see cref="RequestClose"/> 同源，便于区分「用户想关」和「程序要退」。
    /// </summary>
    event Action CloseRequested;
}
