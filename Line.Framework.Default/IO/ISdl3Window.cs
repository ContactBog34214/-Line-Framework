using Line.Framework.IO;
using Line.Framework.IO.Events;
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
    /// 把 <see cref="UnknownEvent"/> 里的原生事件取出来。
    /// <para>
    /// 只有未映射的事件类型才会走 <see cref="UnknownEvent"/>；已映射的
    /// 事件（键盘 / 鼠标 / 触摸…）不保留原生数据 —— 那些字段可能已失效。
    /// </para>
    /// </summary>
    SDL.Event ToSdlEvent(UnknownEvent evt);
}
