using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridTextureView : ITextureView
{
    internal readonly TextureView view;
    public required ITexture TargetTexture { get; init; }

    public uint BaseMipLevel => view.BaseMipLevel;

    public uint MipLevels => view.MipLevels;

    public uint BaseArrayLayer => view.BaseArrayLayer;

    public uint ArrayLayers => view.ArrayLayers;

    public string Name => view.Name;
    internal VeldridTextureView(TextureView v)
    {
        view = v;
    }
    public void Dispose()
    {
        view?.Dispose();
    }
}