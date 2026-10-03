namespace Line.Framework.Graphics;

public struct VertexAttributeDescription
{
    public uint Location { get; set; }
    public PixelFormat Format { get; set; }
    public uint Offset { get; set; }
    public VertexInputRate InputRate { get; set; }
}