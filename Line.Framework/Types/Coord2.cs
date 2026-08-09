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
}
