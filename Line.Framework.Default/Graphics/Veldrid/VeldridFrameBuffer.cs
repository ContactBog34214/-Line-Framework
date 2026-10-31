using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

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

public partial class VeldridConverter
{
    public static global::Veldrid.FramebufferDescription ConvertFramebufferDescription(
        FramebufferCreateInfo createInfo
    )
    {
        return new()
        {
            ColorTargets =
            [
                .. createInfo.ColorTargets.Select(ConvertFramebufferAttachmentDescription),
            ],
            DepthTarget = createInfo.DepthStencilTarget.HasValue
                ? ConvertFramebufferAttachmentDescription(createInfo.DepthStencilTarget.Value)
                : null,
        };
    }

    public static global::Veldrid.FramebufferAttachmentDescription ConvertFramebufferAttachmentDescription(
        Framework.Graphics.FramebufferAttachmentDescription description
    )
    {
        return new()
        {
            ArrayLayer = description.Target.ArrayLayers,
            MipLevel = description.Target.MipLevels,
            Target = ((VeldridTexture)description.Target.TargetTexture).texture,
        };
    }
}

public partial class VeldridResourceFactory
{
    public IFrameBuffer CreateFrameBuffer(FramebufferCreateInfo createInfo)
    {
        var buffer = dev.ResourceFactory.CreateFramebuffer(
            VeldridConverter.ConvertFramebufferDescription(createInfo)
        );
        try
        {
            return new VeldridFrameBuffer(buffer);
        }
        catch
        {
            buffer?.Dispose();
            throw;
        }
    }
}
