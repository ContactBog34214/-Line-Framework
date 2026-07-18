using System.Drawing;
using System.Numerics;

namespace Line.Framework.Graphics;

public struct FullScreenMode
{
    public Vector2 Size { get; init; }
    public float RefreshRate { get; init; }
    public float PixelDensity { get; init; }
    internal nint id;

    public FullScreenMode(Vector2 s, float rfr, float pd)
    {
        Size = s;
        RefreshRate = rfr;
        PixelDensity = pd;
    }
}
