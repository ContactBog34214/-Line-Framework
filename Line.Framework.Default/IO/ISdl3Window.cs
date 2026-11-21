using Line.Framework.IO;
using SDL3;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL3 窗口。
/// <para>
/// <see cref="IHasNativeHandle{T}.NativeHandle"/> 是 <c>SDL_Window*</c>，
/// 需要指针语义时自行 <c>(SDL.Window*)</c> 转换。
/// </para>
/// </summary>
public interface ISdl3Window : IWindow, ISdlWindowSource, IHasNativeHandle<nint>
{
    /// <summary>
    /// 把抽象层的 <see cref="WindowEvent"/> 还原成 SDL 事件。
    /// <para>
    /// 抽象层还没有自己的事件 union，所以现阶段必须靠后端转回来才能读字段。
    /// 这是<b>过渡期的约定</b>，等 core 有了完整事件类型后移除。
    /// </para>
    /// </summary>
    SDL.Event ToSdlEvent(WindowEvent evt);
}
