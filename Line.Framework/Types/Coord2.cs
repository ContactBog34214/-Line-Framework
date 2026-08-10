using System.Numerics;

namespace Line.Framework.Types;

public struct Coord2
{
    /// <summary>
    /// 偏移值
    /// </summary>
    public Vector2 offset { get; set; }

    /// <summary>
    /// 缩放
    /// </summary>
    public Vector2 scale { get; set; }

    public Coord2(Vector2 offset, Vector2 scale)
    {
        this.offset = offset;
        this.scale = scale;
    }

    public static Coord2 operator +(Coord2 a, Coord2 b) =>
        new(a.offset + b.offset, a.scale + b.scale);

    public static Coord2 operator -(Coord2 a, Coord2 b) =>
        new(a.offset - b.offset, a.scale - b.scale);

    public static Coord2 operator *(Coord2 a, float b) => new(a.offset * b, a.scale * b);

    public static Coord2 operator /(Coord2 a, float b) => new(a.offset / b, a.scale / b);
}
