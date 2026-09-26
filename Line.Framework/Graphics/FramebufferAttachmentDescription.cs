namespace Line.Framework.Graphics;

public struct FramebufferAttachmentDescription
{
    public uint MipmapLevel { get; set; }
    public uint ArrayLayer { get; set; }
    public ITexture TargetTexture { get; set; }

}