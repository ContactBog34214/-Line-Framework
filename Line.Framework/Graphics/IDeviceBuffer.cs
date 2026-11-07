using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IDeviceBuffer : IDisposable, IName
{
    ulong SizeInBytes { get; }
    BufferUsage Usage { get; }
    void Update(ulong offset, ReadOnlySpan<byte> source);
}
