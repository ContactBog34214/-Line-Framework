namespace Line.Framework.Graphics;

public struct VertexAttributeDescription
{
    public VertexFormat Format { get; set; }
    public uint Offset { get; set; }
    public uint SizeInBytes { get; set; }
}
