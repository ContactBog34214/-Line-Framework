namespace Line.Framework.Graphics;

[Flags]
public enum BufferUsage
{
    Vertex = 1 << 0,
    Index = 1 << 1,
    Indirect = 1 << 2,
    StorageRead = 1 << 3,
    StorageWrite = 1 << 4,
}