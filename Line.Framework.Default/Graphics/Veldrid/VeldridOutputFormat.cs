using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

public partial class VeldridConverter
{
    /// <summary>
    /// Veldrid 输出描述 → 抽象层输出格式。
    /// <para>
    /// 这是<b>读回</b>方向：由 <see cref="VeldridRenderTarget"/> 从实际的
    /// Framebuffer 上取格式，保证管线用的格式与真实附件一致。
    /// 反方向（抽象层 → Veldrid）由 <c>VeldridPipeline</c> 内部处理，
    /// 因为它还需要处理没有颜色附件时的 fallback 逻辑。
    /// </para>
    /// </summary>
    public static OutputFormatDescription ConvertOutputDescription(
        global::Veldrid.OutputDescription des
    )
    {
        return new()
        {
            ColorAttachments = [.. des.ColorAttachments.Select(ConvertOutputAttachmentDescription)],
            DepthAttachment = des.DepthAttachment.HasValue
                ? ConvertOutputAttachmentDescription(des.DepthAttachment.Value)
                : null,
            SampleCount = ConvertSampleCount(des.SampleCount),
        };
    }

    public static OutputAttachmentDescription ConvertOutputAttachmentDescription(
        global::Veldrid.OutputAttachmentDescription des
    )
    {
        return new() { Format = ConvertPixelFormat(des.Format) };
    }
}
