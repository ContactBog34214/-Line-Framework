using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridFrameBuffer : IFrameBuffer
{
    internal readonly Framebuffer framebuffer;
    public string Name => framebuffer.Name;

    public uint Width => framebuffer.Width;

    public uint Height => framebuffer.Height;

    public OutputFormatDescription OutputFormat { get; }

    public IReadOnlyList<Framework.Graphics.FramebufferAttachmentDescription> ColorTargets { get; }

    public Framework.Graphics.FramebufferAttachmentDescription? DepthTarget { get; }

    public void Dispose()
    {
        framebuffer?.Dispose();
    }

    internal VeldridFrameBuffer(Framebuffer fb, FramebufferCreateInfo createInfo = default)
    {
        framebuffer = fb;

        // OutputFormat 取自 framebuffer 自身，这是权威值：
        // Veldrid 在创建时已经确定了实际的颜色/深度格式与采样数。
        OutputFormat = VeldridConverter.ConvertOutputDescription(fb.OutputDescription);

        // 用 createInfo 判断而不是 fb.DepthTarget.HasValue：
        // 后者为真时 createInfo.DepthStencilTarget 理论上一定有价值，
        // 但那层依赖很脆，直接读 createInfo 不需要 .Value 也不会抛。
        var depth = createInfo.DepthStencilTarget;
        DepthTarget = depth.HasValue
            ? new Framework.Graphics.FramebufferAttachmentDescription
            {
                Target = depth.Value.Target,
            }
            : null;

        // 交换链的 framebuffer 没有 createInfo（由驱动轮转），
        // 此时拿不到 ITextureView，ColorTargets 只能是空列表。
        var colors = createInfo.ColorTargets;
        ColorTargets = colors is { Length: > 0 }
            ?
            [
                .. colors.Select(c => new Framework.Graphics.FramebufferAttachmentDescription
                {
                    Target = c.Target,
                }),
            ]
            : Array.Empty<Framework.Graphics.FramebufferAttachmentDescription>();
    }
}

public partial class VeldridConverter
{
    public static global::Veldrid.OutputDescription ConvertOutputDescription(
        Framework.Graphics.OutputFormatDescription des
    )
    {
        return new()
        {
            ColorAttachments = [.. des.ColorAttachments.Select(ConvertOutputAttachmentDescription)],
            DepthAttachment = des.DepthAttachment.HasValue
                ? ConvertOutputAttachmentDescription(des.DepthAttachment.Value)
                : null,
            SampleCount = ConvertSampleCount(des.SampleCount),
        };
    }

    public static Framework.Graphics.OutputFormatDescription ConvertOutputDescription(
        global::Veldrid.OutputDescription des
    )
    {
        return new()
        {
            ColorAttachments = [.. des.ColorAttachments.Select(ConvertOutputAttachmentDescription)],
            DepthAttachment = des.DepthAttachment.HasValue
                ? ConvertOutputAttachmentDescription(des.DepthAttachment.Value)
                : null,
            SampleCount = ConvertSampleCount(des.SampleCount),
        };
    }

    public static global::Veldrid.OutputAttachmentDescription ConvertOutputAttachmentDescription(
        Framework.Graphics.OutputAttachmentDescription des
    )
    {
        return new() { Format = ConvertPixelFormat(des.Format) };
    }

    public static Framework.Graphics.OutputAttachmentDescription ConvertOutputAttachmentDescription(
        global::Veldrid.OutputAttachmentDescription des
    )
    {
        return new() { Format = ConvertPixelFormat(des.Format) };
    }

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
        if (description.Target is null)
            throw new ArgumentException(
                "FramebufferAttachmentDescription.Target is null.",
                nameof(description)
            );
        if (description.Target.TargetTexture is not VeldridTexture texture)
            throw new ArgumentException(
                $"Attachment target's texture "
                    + $"{description.Target.TargetTexture?.GetType().Name ?? "null"} "
                    + "is not a VeldridTexture.",
                nameof(description)
            );

        return new()
        {
            // 注意：ArrayLayer / MipLevel 是「要附加哪一层 / 哪一级」的**索引**，
            // 不是数量。用 ArrayLayers / MipLevels（数量）会让 1 层 1 mip 的纹理
            // 被解释成第 1 层第 1 级 —— 越界。
            ArrayLayer = description.Target.BaseArrayLayer,
            MipLevel = description.Target.BaseMipLevel,
            Target = texture.texture,
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
            return new VeldridFrameBuffer(buffer, createInfo);
        }
        catch
        {
            buffer?.Dispose();
            throw;
        }
    }
}
