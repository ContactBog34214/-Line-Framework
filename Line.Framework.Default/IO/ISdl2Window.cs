using Line.Framework.IO;
using Line.Framework.IO.Events;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL2 窗口。
/// <para>
/// <see cref="IHasNativeHandle{T}.NativeHandle"/> 是裸 <c>SDL_Window*</c>
/// （SDL2-CS 里窗口就是 <c>IntPtr</c>，没有强类型包装）。
/// </para>
/// <para>
/// <b>与 SDL3 的关键差异</b>：SDL2 的窗口事件统一是
/// <c>SDL_WINDOWEVENT</c>，具体事件在 <c>window.event</c> 子字段里，
/// 因此「哪些事件带窗口 ID」的判断规则与 SDL3 完全不同。
/// </para>
/// </summary>
public interface ISdl2Window : IWindow, IHasNativeHandle<nint>
{
    /// <summary><c>SDL_Window*</c>，与 <see cref="IHasNativeHandle{T}.NativeHandle"/> 同值。</summary>
    nint SdlWindow { get; }

    /// <summary>
    /// 把 <see cref="UnknownEvent"/> 里的原生事件取出来。
    /// <para>只有未映射的事件类型才会走 <see cref="UnknownEvent"/>。</para>
    /// </summary>
    SDL2.SDL.SDL_Event ToSdl2Event(UnknownEvent evt);
}
