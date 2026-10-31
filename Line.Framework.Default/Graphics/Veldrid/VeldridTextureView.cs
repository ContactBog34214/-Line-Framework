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
            throw new InvalidDataException(
                $"Target texture {createInfo.TargetTexture?.GetType().Name ?? "null"} "
                    + "is not a VeldridTexture."
            );

        var target = texture.texture;
        var targetMips = target.MipLevels;
        var targetLayers = target.ArrayLayers;

        // TextureViewCreateInfo 是纯 struct，未赋值的字段都是 0。
        // BaseMipLevel / BaseArrayLayer 的 0 恰好是合法起点，
        // 但 MipLevels / ArrayLayers 的 0 表示「零长度范围」，
        // Veldrid 会抛 "range must be contained in the target Texture"。
        // 因此把这两个的 0 解释为「从 base 一直到末尾」。
        uint baseMip = createInfo.BaseMipLevel;
        uint baseLayer = createInfo.BaseArrayLayer;

        if (baseMip >= targetMips)
            throw new ArgumentOutOfRangeException(
                nameof(createInfo),
                $"BaseMipLevel ({baseMip}) >= target mip levels ({targetMips})."
            );
        if (baseLayer >= targetLayers)
            throw new ArgumentOutOfRangeException(
                nameof(createInfo),
                $"BaseArrayLayer ({baseLayer}) >= target array layers ({targetLayers})."
            );

        uint mipLevels = createInfo.MipLevels == 0 ? targetMips - baseMip : createInfo.MipLevels;
        uint arrayLayers =
            createInfo.ArrayLayers == 0 ? targetLayers - baseLayer : createInfo.ArrayLayers;

        if (baseMip + mipLevels > targetMips)
            throw new ArgumentOutOfRangeException(
                nameof(createInfo),
                $"Mip range [{baseMip}, {baseMip + mipLevels}) exceeds target mip levels ({targetMips})."
            );
        if (baseLayer + arrayLayers > targetLayers)
            throw new ArgumentOutOfRangeException(
                nameof(createInfo),
                $"Array layer range [{baseLayer}, {baseLayer + arrayLayers}) exceeds "
                    + $"target array layers ({targetLayers})."
            );

        var view = dev.ResourceFactory.CreateTextureView(
            new TextureViewDescription()
            {
                ArrayLayers = arrayLayers,
                BaseArrayLayer = baseLayer,
                BaseMipLevel = baseMip,
                Format = createInfo.FormatOverride.HasValue
                    ? VeldridConverter.ConvertPixelFormat(createInfo.FormatOverride.Value)
                    : null,
                MipLevels = mipLevels,
                Target = target,
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
