namespace Line.Framework.Graphics;

public struct TextureViewCreateInfo
{
    /// <summary>
    /// Source texture resource.
    /// </summary>
    public ITexture TargetTexture { get; set; }

    /// <summary>
    /// First mip level to include.
    /// </summary>
    public uint BaseMipLevel { get; set; }

    /// <summary>
    /// Number of mip levels in this view.
    /// </summary>
    public uint MipLevels { get; set; }

    /// <summary>
    /// First array layer to include.
    /// </summary>
    public uint BaseArrayLayer { get; set; }

    /// <summary>
    /// Number of array layers in this view.
    /// </summary>
    public uint ArrayLayers { get; set; }
    public PixelFormat? FormatOverride { get; set; }
}