namespace Line.Framework.Graphics;

[Flags]
public enum TextureUsage
{
    Sampled = 1 << 0,
    ColorTarget = 1 << 1,
    DepthStencilTarget = 1 << 2,
    StorageRead = 1 << 3,
    StorageWrite = 1 << 4,
}