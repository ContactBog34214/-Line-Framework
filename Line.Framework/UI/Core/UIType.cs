using System.Numerics;

public struct Coord2
{
    public Vector2 offset;
    public Vector2 scale;
    public Coord2(Vector2 offset, Vector2 scale)
    {
        this.offset = offset;
        this.scale = scale;
    }
}