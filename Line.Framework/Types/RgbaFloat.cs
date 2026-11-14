using System.Numerics;
using System.Runtime.InteropServices;

namespace Line.Framework.Types;

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct RgbaFloat
{
    /// <summary>
    /// Red
    /// </summary>
    [FieldOffset(0)]
    public float R = 0;

    /// <summary>
    /// Green
    /// </summary>
    [FieldOffset(4)]
    public float G = 0;

    /// <summary>
    /// Blue
    /// </summary>
    [FieldOffset(8)]
    public float B = 0;

    /// <summary>
    /// Alpha
    /// </summary>
    [FieldOffset(12)]
    public float A = 0;

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

    public static implicit operator RgbaFloat(Vector4 vec) =>
        new RgbaFloat(vec.X, vec.Y, vec.Z, vec.W);

    public static RgbaFloat operator +(RgbaFloat a, RgbaFloat b) => (Vector4)a + (Vector4)b;

    public static RgbaFloat operator -(RgbaFloat a, RgbaFloat b) => (Vector4)a - (Vector4)b;

    public static RgbaFloat operator *(RgbaFloat a, RgbaFloat b) => (Vector4)a * (Vector4)b;

    public static RgbaFloat operator /(RgbaFloat a, RgbaFloat b) => (Vector4)a / (Vector4)b;
}
