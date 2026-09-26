namespace Line.Framework.Graphics;

public struct TextureDescription
{
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint ArrayLayers { get; set; }
    public uint MipLevels { get; set; }
    public uint Depth { get; set; }
    public TextureType Type { get; set; }
    public PixelFormats Format { get; set; }
    public TextureSampleCount SampleCount { get; set; }
    public TextureUsage Usage { get; set; }
}