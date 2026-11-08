using System.Runtime.InteropServices;
using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 GPU 缓冲。
/// <para>
/// <b>更新模型</b>：SDL3 的缓冲不能直接写，必须
/// 「transfer buffer → map → 写 → unmap → copy pass 上传」。
/// 因此 <see cref="Update"/> 每次都会额外提交一个命令缓冲。
/// </para>
/// <para>
/// 这决定了调用方式：<b>先攒好整帧数据，再调一次 Update</b>。
/// 每帧多次调用会产生多次 GPU 提交，开销显著。
/// </para>
/// </summary>
public sealed class SDL3Buffer : IDeviceBuffer
{
    private readonly IntPtr dev;
    internal IntPtr handle;
    private readonly ulong sizeInBytes;
    private int disposed;

    public ulong SizeInBytes => sizeInBytes;
    public BufferUsage Usage { get; }
    public string Name { get; internal set; } = "SDL3Buffer";

    internal SDL3Buffer(IntPtr device, IntPtr buffer, ulong size, BufferUsage usage)
    {
        dev = device;
        handle = buffer;
        sizeInBytes = size;
        Usage = usage;
    }

    public void Update(ulong offset, ReadOnlySpan<byte> source)
    {
        if (disposed != 0)
            throw new ObjectDisposedException(nameof(SDL3Buffer));

        ulong end = offset + (ulong)source.Length;
        if (end > sizeInBytes)
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"Update range [{offset}, {end}) exceeds buffer size ({sizeInBytes})."
            );

        if (source.Length == 0)
            return;

        // 1) 建一个临时的 transfer buffer
        var transferInfo = new GPUTransferBufferCreateInfo
        {
            Usage = GPUTransferBufferUsage.Upload,
            Size = (uint)source.Length,
        };
        var transfer = SDL.CreateGPUTransferBuffer(dev, transferInfo);
        if (transfer == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SDL_CreateGPUTransferBuffer failed: {SDL.GetError()}"
            );

        try
        {
            // 2) 映射并写入
            var ptr = SDL.MapGPUTransferBuffer(dev, transfer, false);
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"SDL_MapGPUTransferBuffer failed: {SDL.GetError()}"
                );

            unsafe
            {
                var dst = new Span<byte>((void*)ptr, source.Length);
                source.CopyTo(dst);
            }

            SDL.UnmapGPUTransferBuffer(dev, transfer);

            // 3) copy pass 上传
            var cmd = SDL.AcquireGPUCommandBuffer(dev);
            if (cmd == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"SDL_AcquireGPUCommandBuffer failed: {SDL.GetError()}"
                );

            var copy = SDL.BeginGPUCopyPass(cmd);

            var srcLoc = new GPUTransferBufferLocation { TransferBuffer = transfer, Offset = 0 };
            var dstRegion = new GPUBufferRegion
            {
                Buffer = handle,
                Offset = (uint)offset,
                Size = (uint)source.Length,
            };

            // cycle = true：SDL 会孤儿化原缓冲，避免覆写正在被 GPU 读取的内容
            SDL.UploadToGPUBuffer(copy, srcLoc, dstRegion, true);

            SDL.EndGPUCopyPass(copy);
            SDL.SubmitGPUCommandBuffer(cmd);
        }
        finally
        {
            SDL.ReleaseGPUTransferBuffer(dev, transfer);
        }
    }

    public void Dispose()
    {
        // SDL 的 Release 不支持二次释放；这里用标志保证幂等。
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (handle != IntPtr.Zero)
        {
            SDL.ReleaseGPUBuffer(dev, handle);
            handle = IntPtr.Zero;
        }
    }
}
