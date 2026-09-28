using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IDeviceBuffer:IDisposable,IName
{
    uint SizeInBytes { get; }
    BufferUsage Usage { get; }
}