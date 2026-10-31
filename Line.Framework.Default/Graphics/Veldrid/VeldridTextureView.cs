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

public partial class VeldridResourceFactory
{
    public ITextureView CreateTextureView(TextureViewCreateInfo createInfo)
    {
        if (createInfo.TargetTexture is not VeldridTexture texture)
            throw new InvalidDataException();
        var view = dev.ResourceFactory.CreateTextureView(
            new TextureViewDescription()
            {
                ArrayLayers = createInfo.ArrayLayers,
                BaseArrayLayer = createInfo.BaseArrayLayer,
                BaseMipLevel = createInfo.BaseMipLevel,
                Format = createInfo.FormatOverride.HasValue
                    ? VeldridConverter.ConvertPixelFormat(createInfo.FormatOverride.Value)
                    : null,
                MipLevels = createInfo.MipLevels,
                Target = texture.texture,
            }
        );
        try
        {
            return new VeldridTextureView(view) { TargetTexture = createInfo.TargetTexture };
        }
        catch
        {
            view?.Dispose();
            throw;
        }
    }
}
