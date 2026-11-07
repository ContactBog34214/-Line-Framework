using Line.Framework.Graphics;
using Veldrid;
using BufferUsage = Line.Framework.Graphics.BufferUsage;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridBuffer : IDeviceBuffer
{
    private readonly global::Veldrid.GraphicsDevice dev;
    internal readonly DeviceBuffer buffer;
    public ulong SizeInBytes => buffer.SizeInBytes;

    public BufferUsage Usage { get; }

    public string Name => buffer.Name;

    internal VeldridBuffer(DeviceBuffer bf, BufferUsage usage, global::Veldrid.GraphicsDevice gd)
    {
        buffer = bf;
        Usage = usage;
        dev = gd;
    }

    public void Dispose()
    {
        buffer?.Dispose();
    }

    public void Update(ulong offset, ReadOnlySpan<byte> source)
    {
        ulong end = offset + (ulong)source.Length;
        if (end > SizeInBytes)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Update range [{offset}, {end}) exceeds buffer size ({SizeInBytes})."
            );
        dev.UpdateBuffer(buffer, (uint)offset, source);
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

        if ((usage & BufferUsage.Dynamic) != 0)
            result |= global::Veldrid.BufferUsage.Dynamic;

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

        if ((usage & global::Veldrid.BufferUsage.Dynamic) != 0)
            result |= BufferUsage.Dynamic;

        return result;
    }
}

public partial class VeldridResourceFactory
{
    public IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo)
    {
        if (createInfo.SizeInBytes > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(createInfo));
        var buffer = dev.ResourceFactory.CreateBuffer(
            new()
            {
                SizeInBytes = (uint)createInfo.SizeInBytes,
                StructureByteStride = createInfo.StructureByteStride,
                Usage = VeldridConverter.ConvertBufferUsage(createInfo.Usage),
            }
        );
        try
        {
            return new VeldridBuffer(buffer, createInfo.Usage, dev);
        }
        catch
        {
            buffer?.Dispose();
            throw;
        }
    }
}
