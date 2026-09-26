using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics;

public sealed class VeldridFrameBuffer : IFrameBuffer
{
    private readonly Framebuffer framebuffer;
    public string Name => framebuffer.Name;

    public uint Width => framebuffer.Width;

    public uint Height => framebuffer.Height;

    public void Dispose()
    {
        framebuffer?.Dispose();
    }
    internal VeldridFrameBuffer(Framebuffer fb)
    {
        framebuffer = fb;
    }
}