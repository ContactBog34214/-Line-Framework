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
    PixelFormat Format { get; }
    SampleCount SampleCount { get; }
    TextureUsage Usage { get; }
    void Update(
        ReadOnlySpan<byte> source,
        int x = 0,
        int y = 0,
        int z = 0,
        int? width = null,
        int? height = null,
        int? depth = null,
        uint mipLevel = 0,
        uint arrayLayer = 0
    );
}
