using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;
using PixelFormat = Line.Framework.Graphics.PixelFormat;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 GPU 纹理。
/// </summary>
public sealed class SDL3Texture : ITexture
{
    private readonly IntPtr dev;
    internal IntPtr handle;
    private int disposed;

    public uint Width { get; }
    public uint Height { get; }
    public uint ArrayLayers { get; }
    public uint MipLevels { get; }
    public uint Depth { get; }
    public TextureType Type { get; }
    public PixelFormat Format { get; }
    public SampleCount SampleCount { get; }
    public TextureUsage Usage { get; }
    public string Name { get; internal set; } = "SDL3Texture";

    internal SDL3Texture(IntPtr device, IntPtr texture, TextureCreateInfo ci)
    {
        dev = device;
        handle = texture;
        Width = ci.Width;
        Height = ci.Height == 0 ? 1u : ci.Height;
        ArrayLayers = ci.ArrayLayers == 0 ? 1u : ci.ArrayLayers;
        MipLevels = ci.MipLevels == 0 ? 1u : ci.MipLevels;
        Depth = ci.Depth == 0 ? 1u : ci.Depth;
        Type = ci.Type == 0 ? TextureType.Texture2D : ci.Type;
        Format = ci.Format;
        SampleCount = ci.SampleCount == 0 ? SampleCount.Count1 : ci.SampleCount;
        Usage = ci.Usage;
    }

    public void Update(
        ReadOnlySpan<byte> source,
        int x = 0,
        int y = 0,
        int z = 0,
        int? width = null,
        int? height = null,
        int? depth = null,
        uint mipLevel = 0,
        uint arrayLayer = 0
    )
    {
        if (disposed != 0)
            throw new ObjectDisposedException(nameof(SDL3Texture));

        // 目标区域的上界必须按「该 mip 层」的尺寸算，不是基础层。
        // 用基础层尺寸校验 mip1 的数据会判定为「长度不足」（mip1 只有 1/4 大）。
        uint mipW = MipmapGenerator.GetMipDimension(Width, mipLevel);
        uint mipH = MipmapGenerator.GetMipDimension(Height, mipLevel);
        uint mipD = MipmapGenerator.GetMipDimension(Depth, mipLevel);

        int w = width ?? (int)mipW - x;
        int h = height ?? (int)mipH - y;
        // 与 VeldridTexture.Update 保持一致：不指定 depth 时上传「从 z 到该层末尾」。
        // 若这里只取 1，传入完整体积的数据就会被下面的长度校验拒绝，
        // 同一份 TextureCreateInfo + Update 调用在两个后端上行为不同。
        int d = depth ?? (int)mipD - z;

        if (x < 0 || y < 0 || z < 0)
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"(x,y,z) = ({x},{y},{z}) must be non-negative."
            );
        if (w <= 0 || h <= 0 || d <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Region size ({w},{h},{d}) must be positive; "
                    + $"mip{mipLevel} is {mipW}x{mipH}x{mipD}."
            );
        if (x + w > mipW || y + h > mipH || z + d > mipD)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Region [{x},{x + w})x[{y},{y + h})x[{z},{z + d}) "
                    + $"exceeds mip{mipLevel} size {mipW}x{mipH}x{mipD}."
            );

        ulong expected = Format.GetByteSize((uint)w, (uint)h, (uint)d);
        if ((ulong)source.Length != expected)
            throw new ArgumentException(
                $"Data length {source.Length} != {w}x{h}x{d} @ "
                    + $"{Format.GetBytesPerPixel()}B/texel = {expected}.",
                nameof(source)
            );

        // transfer buffer → map → copy → upload，与 Buffer 相同的三步
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

            var cmd = SDL.AcquireGPUCommandBuffer(dev);
            if (cmd == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"SDL_AcquireGPUCommandBuffer failed: {SDL.GetError()}"
                );

            var copy = SDL.BeginGPUCopyPass(cmd);

            var srcLoc = new GPUTextureTransferInfo { TransferBuffer = transfer, Offset = 0 };
            var dstRegion = new GPUTextureRegion
            {
                Texture = handle,
                MipLevel = mipLevel,
                Layer = arrayLayer,
                X = (uint)x,
                Y = (uint)y,
                Z = (uint)z,
                W = (uint)w,
                H = (uint)h,
                D = (uint)d,
            };

            // cycle = false：<b>必须</b>。
            //
            // cycle = true 会让 SDL 孤儿化目标纹理 —— 分配新内存并丢弃旧内容。
            // 对「一次性覆写整张纹理」是对的，但 Update 的语义是「更新区域」：
            // 上传 mip 链时每次调用都会把之前写好的层清掉，最后只剩最后一个 mip。
            // 表现是纹理全透明、不报错（因为数据长度和格式都合法）。
            //
            // write-after-read 的顾虑不适用于本框架的用法：新建纹理在
            // 上传期间没有任何 GPU 读取，运行时局部更新由调用方自己保证同步。
            SDL.UploadToGPUTexture(copy, srcLoc, dstRegion, false);

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
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (handle != IntPtr.Zero)
        {
            SDL.ReleaseGPUTexture(dev, handle);
            handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// SDL3 GPU 采样器。
/// </summary>
public sealed class SDL3Sampler : ISampler
{
    private readonly IntPtr dev;
    internal IntPtr handle;
    private int disposed;

    public string Name { get; internal set; } = "SDL3Sampler";

    internal SDL3Sampler(IntPtr device, IntPtr sampler)
    {
        dev = device;
        handle = sampler;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (handle != IntPtr.Zero)
        {
            SDL.ReleaseGPUSampler(dev, handle);
            handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// SDL3 GPU 着色器。
/// </summary>
public sealed class SDL3Shader : IShader
{
    private readonly IntPtr dev;
    internal IntPtr handle;
    private int disposed;

    public string Name { get; internal set; } = "SDL3Shader";
    public string EntryPoint { get; internal set; } = "main";
    public ShaderStage Stage { get; internal set; }

    internal SDL3Shader(IntPtr device, IntPtr shader, ShaderStage stage)
    {
        dev = device;
        handle = shader;
        Stage = stage;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (handle != IntPtr.Zero)
        {
            SDL.ReleaseGPUShader(dev, handle);
            handle = IntPtr.Zero;
        }
    }
}
