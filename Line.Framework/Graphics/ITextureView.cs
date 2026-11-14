using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface ITextureView : IDisposable, IName
{
    ITexture TargetTexture { get; }

    uint BaseMipLevel { get; }

    uint MipLevels { get; }

    uint BaseArrayLayer { get; }

    uint ArrayLayers { get; }
}