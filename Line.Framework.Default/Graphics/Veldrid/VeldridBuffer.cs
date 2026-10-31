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

public partial class VeldridConverter
{
    public static global::Veldrid.BufferUsage ConvertBufferUsage(BufferUsage usage)
    {
        global::Veldrid.BufferUsage result = 0;

        if ((usage & BufferUsage.Vertex) != 0)
            result |= global::Veldrid.BufferUsage.VertexBuffer;

        if ((usage & BufferUsage.Index) != 0)
            result |= global::Veldrid.BufferUsage.IndexBuffer;

        if ((usage & BufferUsage.Indirect) != 0)
            result |= global::Veldrid.BufferUsage.IndirectBuffer;

        if ((usage & BufferUsage.StorageWrite) != 0)
            result |= global::Veldrid.BufferUsage.StructuredBufferReadWrite;
        else if ((usage & BufferUsage.StorageRead) != 0)
            result |= global::Veldrid.BufferUsage.StructuredBufferReadOnly;

        return result;
    }

    public static BufferUsage ConvertBufferUsage(global::Veldrid.BufferUsage usage)
    {
        BufferUsage result = 0;

        if ((usage & global::Veldrid.BufferUsage.VertexBuffer) != 0)
            result |= BufferUsage.Vertex;

        if ((usage & global::Veldrid.BufferUsage.IndexBuffer) != 0)
            result |= BufferUsage.Index;

        if ((usage & global::Veldrid.BufferUsage.IndirectBuffer) != 0)
            result |= BufferUsage.Indirect;

        if ((usage & global::Veldrid.BufferUsage.StructuredBufferReadWrite) != 0)
        {
            result |= BufferUsage.StorageRead;
            result |= BufferUsage.StorageWrite;
        }
        else if ((usage & global::Veldrid.BufferUsage.StructuredBufferReadOnly) != 0)
        {
            result |= BufferUsage.StorageRead;
        }

        return result;
    }
}

public partial class VeldridResourceFactory
{
    public IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo)
    {
        var buffer = dev.ResourceFactory.CreateBuffer(
            new()
            {
                SizeInBytes = createInfo.SizeInBytes,
                StructureByteStride = createInfo.StructureByteStride,
                Usage = VeldridConverter.ConvertBufferUsage(createInfo.Usage),
            }
        );
        try
        {
            return new VeldridBuffer(buffer, createInfo.Usage);
        }
        catch
        {
            buffer?.Dispose();
            throw;
        }
    }
}
