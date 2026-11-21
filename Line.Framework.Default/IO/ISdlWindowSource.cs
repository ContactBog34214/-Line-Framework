namespace Line.Framework.Default.IO;

/// <summary>
/// 能提供 <c>SDL_Window*</c> 的对象。
/// <para>
/// 比 <see cref="ISdl3Window"/> 更宽松：第三方窗口类只要实现这<b>一个成员</b>
/// 就能接到 SDL3 GPU 后端上，不必完整实现 <c>IWindow</c>。
/// </para>
/// <para>
/// SDL3 后端判断「能不能用这个窗口」时一律看本接口，
/// 而不是 <c>is SDL3Window</c> —— 后者把用户锁死在我们的实现类上。
/// </para>
/// </summary>
public interface ISdlWindowSource
{
    /// <summary><c>SDL_Window*</c>；为 0 时视为不可用。</summary>
    nint SdlWindow { get; }
}
