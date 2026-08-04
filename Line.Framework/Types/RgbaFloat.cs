using System.Numerics;

namespace Line.Framework.Types;

public struct RgbaFloat
{
    public float R { get; set; } = 0;
    public float G { get; set; } = 0;
    public float B { get; set; } = 0;
    public float A { get; set; } = 0;

    public RgbaFloat(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public RgbaFloat(int r, int g, int b, int a)
    {
        R = r / 255f;
        G = g / 255f;
        B = b / 255f;
        A = a / 255f;
    }

    public static implicit operator Vector4(RgbaFloat rgba) =>
        new Vector4(rgba.R, rgba.G, rgba.B, rgba.A);

    public static implicit operator Veldrid.RgbaFloat(RgbaFloat rgba) =>
        new Veldrid.RgbaFloat(rgba.R, rgba.G, rgba.B, rgba.A);

    public static implicit operator RgbaFloat(Veldrid.RgbaFloat rgba) =>
        new RgbaFloat(rgba.R, rgba.G, rgba.B, rgba.A);

    public static implicit operator RgbaFloat(Vector4 vec) =>
        new RgbaFloat(vec.X, vec.Y, vec.Z, vec.W);
}
