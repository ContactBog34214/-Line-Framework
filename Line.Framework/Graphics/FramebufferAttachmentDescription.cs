namespace Line.Framework.Graphics;

/// <summary>
/// 帧缓冲的一个附件：指向某个纹理视图。
/// </summary>
public struct FramebufferAttachmentDescription
{
    /// <summary>作为附件使用的纹理视图。必须显式赋值（required）。</summary>
    public required ITextureView Target { get; set; }
}
