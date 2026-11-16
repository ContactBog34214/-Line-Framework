namespace Line.Framework.Graphics;

/// <summary>
/// 输出附件格式（只描述格式，不含纹理引用）。
/// <para>
/// 管线需要知道渲染目标的格式才能正确创建，
/// 而格式必须与交换链或帧缓冲的附件格式一致。
/// </para>
/// </summary>
public struct OutputAttachmentDescription
{
    /// <summary>该附件的像素格式。</summary>
    public PixelFormat Format { get; set; }
}
