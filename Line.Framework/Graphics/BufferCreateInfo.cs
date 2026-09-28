namespace Line.Framework.Graphics;

public struct BufferCreateInfo
{
    public uint SizeInBytes { get; set; }
    public BufferUsage Usage { get; set; }
    public uint StructureByteStride { get; set; }
}