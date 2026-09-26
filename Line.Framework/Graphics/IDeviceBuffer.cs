using Line.Framework.Types;

namespace Line.Framework.Graphics;

interface IDeviceBuffer:IDisposable,IName
{
    uint SizeInBytes { get; }
    BufferUsage Usage { get; }
}