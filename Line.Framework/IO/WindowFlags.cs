namespace Line.Framework.IO;

/// <summary>
/// 窗口创建标志（与后端无关）。
/// <para>
/// <b>为什么另立一套</b>：<c>SDL.WindowFlags</c> 是 SDL3 的 ABI 细节 ——
/// 它的数值、成员名都会随 SDL 版本变化，而且其中一半（OpenGL / Vulkan / Metal）
/// 对我们的 Veldrid 后端毫无意义、对 SDL GPU 后端则是有害的干扰项。
/// 抽象层只暴露「任何后端都应该能表达」的那部分。
/// </para>
/// <para>
/// <b>刻意不包含</b>：
/// <list type="bullet">
/// <item><c>OpenGL</c> / <c>Vulkan</c> / <c>Metal</c> —— Veldrid 自己建 surface
///       不需要窗口标志协助；SDL3 GPU 则会因为看到这些位而拒绝建链。
///       历史事故就是这么来的（把 Vulkan 传进去，SDL_ClaimWindowForGPUDevice
///       报「不支持透明窗口」）。</item>
/// <item><c>Grabbed</c> 类 —— SDL3-CS 里叫 <c>MouseGrabbed</c> 还是
///       <c>KeyboardGrabbed</c> 需要核对，先不暴露，避免写错名字后静默丢标志。</item>
/// </list>
/// </para>
/// </summary>
[Flags]
public enum WindowFlags
{
    /// <summary>无标志。</summary>
    None = 0,

    /// <summary>创建时不显示。</summary>
    Hidden = 1 << 0,

    /// <summary>可调整大小。</summary>
    Resizable = 1 << 1,

    /// <summary>无边框。</summary>
    Borderless = 1 << 2,

    /// <summary>始终置顶。</summary>
    AlwaysOnTop = 1 << 3,

    /// <summary>全屏创建。</summary>
    Fullscreen = 1 << 4,

    /// <summary>
    /// 请求高 DPI 后备缓冲（<see cref="IWindow.PixelSize"/> 会大于逻辑尺寸）。
    /// <para>
    /// 不设这个标志时，SDL 可能让像素尺寸等于逻辑尺寸，**整个 HiDPI 路径失效**。
    /// </para>
    /// </summary>
    HighPixelDensity = 1 << 5,

    /// <summary>创建时立即请求输入焦点。</summary>
    InputFocus = 1 << 6,

    /// <summary>创建时立即请求鼠标焦点。</summary>
    MouseFocus = 1 << 7,

    /// <summary>允许透明后备缓冲。SDL3 GPU 后端会明确拒绝带此标志的窗口。</summary>
    Transparent = 1 << 8,

    /// <summary>工具窗口（不占任务栏、随父窗口最小化）。</summary>
    Utility = 1 << 9,

    /// <summary>提示气泡式窗口。</summary>
    Tooltip = 1 << 10,

    /// <summary>弹出菜单式窗口。</summary>
    PopupMenu = 1 << 11,

    /// <summary>不接受焦点（点击不抢焦点）。</summary>
    NotFocusable = 1 << 12,
}
