using System.Numerics;

namespace Line.Framework.Types;

public struct RgbaFloat
{
    /// <summary>
    /// Red
    /// </summary>
    public float R
    {
        get;
        set { field = Math.Clamp(value, 0f, 1f); }
    } = 0;

    /// <summary>
    /// Green
    /// </summary>
    public float G
    {
        get;
        set { field = Math.Clamp(value, 0f, 1f); }
    } = 0;

    /// <summary>
    /// Blue
    /// </summary>
    public float B
    {
        get;
        set { field = Math.Clamp(value, 0f, 1f); }
    } = 0;

    /// <summary>
    /// Alpha
    /// </summary>
    public float A
    {
        get;
        set { field = Math.Clamp(value, 0f, 1f); }
    } = 0;

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

    public static RgbaFloat operator +(RgbaFloat a, RgbaFloat b) => (Vector4)a + (Vector4)b;

    public static RgbaFloat operator -(RgbaFloat a, RgbaFloat b) => (Vector4)a - (Vector4)b;

    public static RgbaFloat operator *(RgbaFloat a, RgbaFloat b) => (Vector4)a * (Vector4)b;

    public static RgbaFloat operator /(RgbaFloat a, RgbaFloat b) => (Vector4)a / (Vector4)b;
}
