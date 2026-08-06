using System.Numerics;
using Line.Framework.Types;
using Veldrid;

namespace Line.Framework.Graphics;

public class Vertex
{
    public Vector2 Position { get; set; }
    public Types.RgbaFloat Color { get; set; }
    public Coord2 UV { get; set; }
    public Texture Texture { get; set; }
    public ResourceSet ResourceSet { get; set; }
    public float Opacity { get; set; }
    public List<Vector2[]> Clips { get; set; } = new();

    public Vertex(Vector2 p, Types.RgbaFloat c, Coord2 u, Texture t, ResourceSet rs, float o)
    {
        Position = p;
        Color = c;
        UV = u;
        Texture = t;
        ResourceSet = rs;
        Opacity = o;
    }
}
