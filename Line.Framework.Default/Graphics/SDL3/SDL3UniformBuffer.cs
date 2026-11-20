using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 的 uniform「缓冲」。
/// <para>
/// SDL_GPU 没有可绑定的 uniform buffer，也没有 <c>SDL_BindGPU*UniformBuffers</c>：
/// uniform 数据通过 <c>SDL_PushGPU{Vertex,Fragment}UniformData</c> 在每次
/// 绑定资源集时推送进命令缓冲。
/// </para>
/// <para>
/// 因此本类只是 CPU 侧的一份影子拷贝，<b>不持有任何 GPU 资源</b>：
/// <see cref="Update"/> 是纯内存写入，没有 transfer buffer、没有 copy pass，
/// 比走 GPU 缓冲便宜得多。真正上传发生在
/// <see cref="SDL3CommandList.SetGraphicsResourceSet"/> 里。
/// </para>
/// <para>
/// 因此它也不能用于顶点/索引等几何用途——
/// 那些必须是真的 GPU 缓冲（见 <see cref="SDL3ResourceFactory.CreateBuffer"/>）。
/// </para>
/// </summary>
public sealed class SDL3UniformBuffer : IDeviceBuffer
{
    private readonly byte[] data;
    private int disposed;

    public ulong SizeInBytes => (ulong)data.Length;
    public BufferUsage Usage { get; }
    public string Name { get; internal set; } = "SDL3Uniform";

    internal SDL3UniformBuffer(uint sizeInBytes, BufferUsage usage)
    {
        if (sizeInBytes == 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

        data = new byte[sizeInBytes];
        Usage = usage;
    }

    /// <summary>推送源：整块内容。Block 大小以布局声明为准，这里不做裁剪。</summary>
    internal ReadOnlySpan<byte> Bytes => data;

    public void Update(ulong offset, ReadOnlySpan<byte> source)
    {
        if (disposed != 0)
            throw new ObjectDisposedException(nameof(SDL3UniformBuffer));

        ulong end = offset + (ulong)source.Length;
        if (end > SizeInBytes)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Update range [{offset}, {end}) exceeds buffer size ({SizeInBytes})."
            );

        if (source.Length == 0)
            return;

        source.CopyTo(data.AsSpan((int)offset));
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref disposed, 1);
    }
}
