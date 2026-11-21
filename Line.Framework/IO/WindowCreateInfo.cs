namespace Line.Framework.IO;

/// <summary>
/// 创建窗口的参数。
/// <para>
/// 它的存在是为了让 <c>IWindow</c> 的实现（如 <c>SDL3Window</c>）的公开构造签名
/// 不出现任何后端类型 —— 否则「窗口是后端无关的」这条分层在第一步就破了。
/// </para>
/// </summary>
public struct WindowCreateInfo
{
    /// <summary>窗口标题。</summary>
    public string Title { get; set; }

    /// <summary>逻辑宽度。</summary>
    public int Width { get; set; }

    /// <summary>逻辑高度。</summary>
    public int Height { get; set; }

    /// <summary>
    /// 创建标志。
    /// <para>
    /// 缺省是 <c>Resizable | InputFocus | MouseFocus</c>。
    /// <b>不要</b>手写 OpenGL / Vulkan / Metal 之类 —— 抽象层也没有这些成员。
    /// </para>
    /// </summary>
    public WindowFlags Flags { get; set; }

    /// <summary>
    /// 常用的默认值：640×480、可缩放、创建时抢焦点。
    /// </summary>
    public static WindowCreateInfo Default =>
        new()
        {
            Title = "Title",
            Width = 640,
            Height = 480,
            Flags = WindowFlags.Resizable | WindowFlags.InputFocus | WindowFlags.MouseFocus,
        };

    /// <summary>
    /// 修正非法尺寸 —— 传 0 或负数时回落到默认值。
    /// 各后端都该在创建前做这件事，所以放在这里统一。
    /// </summary>
    public WindowCreateInfo WithSanitizedSize()
    {
        var copy = this;
        if (copy.Width <= 0)
            copy.Width = 640;
        if (copy.Height <= 0)
            copy.Height = 480;

        if (string.IsNullOrEmpty(copy.Title))
            copy.Title = "Title";

        return copy;
    }
}
