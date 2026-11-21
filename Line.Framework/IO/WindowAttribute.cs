namespace Line.Framework.IO;

/// <summary>
/// 窗口的可选属性键。
/// <para>
/// <b>为什么分两套</b>：被 core 其它模块编译期依赖的（尺寸、标题、焦点）走
/// <see cref="IWindow"/> 的强类型属性；「窗口级开关 / 偏好」走这张表。
/// 这样以后加「置顶 / 透明度 / 新平台开关」时，<see cref="IWindow"/> 不必跟着长。
/// </para>
/// <para>
/// <b>为什么用 enum</b>：字符串键拼错只能运行期发现，enum 拼错是编译错误；
/// 而且 enum 能挂一份注册表（期望类型 + 默认值），实现侧靠它做校验与兜底。
/// </para>
/// <para>
/// 不命名为 <c>Attribute</c>，避免与 <see cref="System.Attribute"/> 撞名。
/// </para>
/// </summary>
public enum WindowAttribute
{
    /// <summary>是否显示光标。<c>bool</c>，默认 true。</summary>
    ShowCursor,

    /// <summary>相对鼠标模式（FPS 式抓取）。<c>bool</c>，默认 false。</summary>
    RelativeMouse,

    /// <summary>相对鼠标灵敏度倍率，必须 &gt; 0。<c>float</c>，默认 1。</summary>
    MouseSpeedScale,

    /// <summary>是否接受文本输入。<c>bool</c>，默认 false。</summary>
    TextInput,

    /// <summary>窗口置顶。<c>bool</c>，默认 false。</summary>
    AlwaysOnTop,

    /// <summary>窗口不透明度，0~1。<c>float</c>，默认 1。</summary>
    Opacity,
}
