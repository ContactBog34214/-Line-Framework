namespace Line.Framework.Graphics;

public struct TextureCreateInfo
{
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint ArrayLayers { get; set; }
    public uint MipLevels { get; set; }
    public uint Depth { get; set; }
    public TextureType Type { get; set; }
    public PixelFormat Format { get; set; }
    public SampleCount SampleCount { get; set; }
    public TextureUsage Usage { get; set; }
}