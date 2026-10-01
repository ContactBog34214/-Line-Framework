using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridTexture : ITexture
{
    internal readonly global::Veldrid.Texture texture;
    public uint Width => texture.Width;

    public uint Height => texture.Height;

    public uint ArrayLayers => texture.ArrayLayers;

    public uint MipLevels => texture.MipLevels;

    public uint Depth => texture.Depth;

    public TextureType Type { get; }

    public PixelFormat Format { get; }

    public SampleCount SampleCount { get; }

    public TextureUsage Usage { get; }

    public string Name => texture.Name;
    internal VeldridTexture(global::Veldrid.Texture source)
    {
        texture = source;
        Type = VeldridConverter.ConvertTextureType(source.Type);
        Format = VeldridConverter.ConvertPixelFormat(source.Format);
        SampleCount = VeldridConverter.ConvertSampleCount(source.SampleCount);
        Usage = VeldridConverter.ConvertTextureUsage(source.Usage);
    }
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}