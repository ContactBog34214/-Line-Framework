using System.Numerics;

namespace Line.Framework.Types;

public struct Coord2
{
    public Vector2 offset { get; set; }
    public Vector2 scale { get; set; }

    public Coord2(Vector2 offset, Vector2 scale)
    {
        this.offset = offset;
        this.scale = scale;
    }
}
