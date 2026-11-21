namespace Line.Framework.Default.IO;

/// <summary>
/// 能提供一个「裸」原生句柄。
/// <para>
/// 指针语义：借用方必须知道它到底是什么（HWND / wl_surface* / NSWindow* …），
/// 并自行转换类型。拿错句柄在原生层通常只会得到一句完全无关的报错，
/// 因此需要类型标签时应使用 <see cref="IHasNativeHandle{T}"/>。
/// </para>
/// </summary>
public interface IHasNativeHandle
{
    /// <summary>原生句柄。</summary>
    nint NativeHandle { get; }
}

/// <summary>
/// 带类型标签的原生句柄。
/// <para>
/// 泛型参数的作用是<b>标签</b>而不是「多一个成员」：它防的是
/// 「把 <c>SDL_Window*</c> 当 <c>wl_display*</c> 传」这类错误。
/// 那种错误在原生层只会表现为一句完全无关的报错
/// （历史事故：<c>wl_surface*</c> 被当窗口，报「不支持透明窗口」）。
/// </para>
/// <para>
/// <b>本接口必须留在 Default</b>：core 一旦认识「原生句柄」，core 就绑上 SDL 了。
/// </para>
/// </summary>
public interface IHasNativeHandle<out T> : IHasNativeHandle
{
    /// <summary>强类型标签下的原生句柄。</summary>
    new T NativeHandle { get; }
}
