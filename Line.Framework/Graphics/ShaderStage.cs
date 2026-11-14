namespace Line.Framework.Graphics;

[Flags]
public enum ShaderStage
{
    None = 0,
    Vertex = 1 << 0,
    Fragment = 1 << 1,
}
