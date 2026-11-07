namespace Line.Framework.Graphics;

public struct BufferCreateInfo
{
    public ulong SizeInBytes { get; set; }
    public BufferUsage Usage { get; set; }
    public uint StructureByteStride { get; set; }
}
