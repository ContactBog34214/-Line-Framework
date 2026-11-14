using Line.Framework.Types;

namespace Line.Framework.Graphics;

public struct BlendStateDescription
{
    public RgbaFloat BlendFactor { get; set; }
    public BlendAttachmentDescription[] AttachmentStates { get; set; }
    public bool AlphaToCoverageEnabled { get; set; }
    public static readonly BlendStateDescription SingleOverrideBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.OverrideBlend],
    };
    public static readonly BlendStateDescription SingleAlphaBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.AlphaBlend],
    };
    public static readonly BlendStateDescription SingleAdditiveBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.AdditiveBlend],
    };
    public static readonly BlendStateDescription SingleDisabled = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.Disabled],
    };
}
