namespace Line.Framework.Graphics;

/// <summary>
/// 创建离屏帧缓冲的参数。
/// <para>
/// <b>SDL3 后端不支持</b>：它没有帧缓冲对象，渲染目标就是纹理本身，
/// 因此 <see cref="IResourceFactory.CreateFrameBuffer"/> 在该后端会抛
/// <see cref="NotSupportedException"/>。需要跨后端工作时，
/// 离屏渲染方案必须重新设计（尚未接入抽象层）。
/// </para>
/// </summary>
public struct FramebufferCreateInfo
{
    /// <summary>可选深度 / 模板附件。</summary>
    public FramebufferAttachmentDescription? DepthStencilTarget { get; set; }

    /// <summary>颜色附件清单，顺序即颜色附件的 index。</summary>
    public FramebufferAttachmentDescription[] ColorTargets { get; set; }
}
