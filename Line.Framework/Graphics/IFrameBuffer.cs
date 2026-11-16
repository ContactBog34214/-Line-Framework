using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 帧缓冲：一次渲染的目标集合（若干颜色目标 + 可选深度目标）。
/// <para>
/// <b>后端差异</b>：Veldrid 有持久对象，<see cref="ColorTargets"/> 是真实数据；
/// SDL3 的渲染目标是每帧从窗口「取」到的纹理，没有帧缓冲对象，
/// 因此 <see cref="ColorTargets"/> 返回空数组、<see cref="DepthTarget"/> 恒为 null。
/// </para>
/// <para>
/// <b>因此不要遍历 <see cref="ColorTargets"/> 去推导管线格式</b> ——
/// 需要输出格式时一律读 <see cref="OutputFormat"/>，它在两个后端都有效。
/// 交换链的帧缓冲由 <see cref="ISwapchain.FrameBuffer"/> 提供，
/// 不要自行创建（SDL3 会直接抛 <see cref="NotSupportedException"/>）。
/// </para>
/// </summary>
public interface IFrameBuffer : IDisposable, IName
{
    /// <summary>宽度（像素）。交换链目标下可能随窗口变化。</summary>
    uint Width { get; }

    /// <summary>高度（像素）。</summary>
    uint Height { get; }

    /// <summary>
    /// 输出附件格式集合。<b>这是构建管线时唯一可靠的格式来源</b>，
    /// 在两个后端都有效。
    /// </summary>
    OutputFormatDescription OutputFormat { get; }

    /// <summary>
    /// 颜色附件清单。SDL3 后端返回空数组（它没有帧缓冲对象）。
    /// </summary>
    IReadOnlyList<FramebufferAttachmentDescription> ColorTargets { get; }

    /// <summary>深度附件；无深度缓冲时为 null。</summary>
    FramebufferAttachmentDescription? DepthTarget { get; }
}
