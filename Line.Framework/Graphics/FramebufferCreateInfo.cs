namespace Line.Framework.Graphics;

public struct FramebufferCreateInfo
{
    public FramebufferAttachmentDescription? DepthStencilTarget { get; set; }
    public FramebufferAttachmentDescription[] ColorTargets { get; set; }
}