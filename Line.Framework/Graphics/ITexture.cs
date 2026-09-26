using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface ITexture : IDisposable, IName
{
    uint Width { get; }
    uint Height { get; }
    uint ArrayLayers { get; }
    uint MipLevels { get; }
    uint Depth { get; }
    TextureType Type { get; }
    PixelFormats Format { get; }
    TextureSampleCount SampleCount { get; }
    TextureUsage Usage { get; }
}