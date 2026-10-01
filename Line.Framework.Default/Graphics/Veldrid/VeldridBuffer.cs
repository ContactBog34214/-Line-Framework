using Line.Framework.Graphics;
using Veldrid;
using BufferUsage = Line.Framework.Graphics.BufferUsage;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridBuffer : IDeviceBuffer
{
    internal readonly DeviceBuffer buffer;
    public uint SizeInBytes => buffer.SizeInBytes;

    public BufferUsage Usage { get; }

    public string Name => buffer.Name;
    internal VeldridBuffer(DeviceBuffer bf, BufferUsage usage)
    {
        buffer = bf;
        Usage = usage;
    }

    public void Dispose()
    {
        buffer?.Dispose();
    }
}