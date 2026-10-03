using Line.Framework.Graphics;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridResourceFactory : IResourceFactory
{
    private readonly GraphicsDevice dev;
    public IShader CreateShader(ShaderCreateInfo createInfo)
    {
        var shader = dev.ResourceFactory.CreateFromSpirv(new()
        {
            EntryPoint = createInfo.EntryPoint,
            Stage = VeldridShader.GetStages(createInfo.Stage),
            ShaderBytes = createInfo.Bytes
        });
        try
        {
            return new VeldridShader(shader);
        }
        catch
        {
            shader?.Dispose();
            throw;
        }
    }

    public IFrameBuffer CreateFrameBuffer()
    {
        throw new NotImplementedException();
    }

    public IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo)
    {
        var buffer = dev.ResourceFactory.CreateBuffer(new()
        {
            SizeInBytes = createInfo.SizeInBytes,
            StructureByteStride = createInfo.StructureByteStride,
            Usage = VeldridConverter.ConvertBufferUsage(createInfo.Usage),
        });
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

    public ITexture CreateTexture(TextureCreateInfo createInfo)
    {
        var texture = dev.ResourceFactory.CreateTexture(new()
        {
            ArrayLayers = createInfo.ArrayLayers,
            Depth = createInfo.Depth,
            Format = VeldridConverter.ConvertPixelFormat(createInfo.Format),
            Height = createInfo.Height,
            MipLevels = createInfo.MipLevels,
            SampleCount = VeldridConverter.ConvertSampleCount(createInfo.SampleCount),
            Type = VeldridConverter.ConvertTextureType(createInfo.Type),
            Usage = VeldridConverter.ConvertTextureUsage(createInfo.Usage),
            Width = createInfo.Width,
        });
        try
        {
            return new VeldridTexture(texture);
        }
        catch
        {
            texture?.Dispose();
            throw;
        }
    }

    public ISampler CreateSampler(SamplerCreateInfo createInfo)
    {
        var sampler = dev.ResourceFactory.CreateSampler(new()
        {
            AddressModeU = VeldridConverter.ConvertSamplerAddressMode(createInfo.AddressU),
            AddressModeV = VeldridConverter.ConvertSamplerAddressMode(createInfo.AddressV),
            AddressModeW = VeldridConverter.ConvertSamplerAddressMode(createInfo.AddressW),
            Filter = createInfo.AnisotropyEnabled ? SamplerFilter.Anisotropic : VeldridConverter.ConvertSamplerFilter(
                createInfo.MinFilter,
                createInfo.MagFilter,
                createInfo.MipmapMode
            ),
            LodBias = createInfo.LodBias,
            MaximumAnisotropy = createInfo.MaxAnisotropy,
            MaximumLod = createInfo.MaxLod,
            MinimumLod = createInfo.MinLod,
        });
        try
        {
            return new VeldridSampler(sampler);
        }
        catch
        {
            sampler?.Dispose();
            throw;
        }
    }

    public IFrameBuffer CreateFrameBuffer(FramebufferCreateInfo createInfo)
    {
        var buffer = dev.ResourceFactory.CreateFramebuffer(new()
        {
            ColorTargets = [.. createInfo.ColorTargets.Select(VeldridConverter.ConvertFramebufferAttachmentDescription)],
            DepthTarget = createInfo.DepthStencilTarget.HasValue
                ? VeldridConverter.ConvertFramebufferAttachmentDescription(createInfo.DepthStencilTarget.Value)
                : null
        });
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

    public IPipeline CreatePipeline(PipelineCreateInfo createInfo)
    {
        throw new NotImplementedException();
    }

    public ITextureView CreateTextureView(TextureViewCreateInfo createInfo)
    {
        if (createInfo.TargetTexture is not VeldridTexture texture) throw new InvalidDataException();
        var view = dev.ResourceFactory.CreateTextureView(new TextureViewDescription()
        {
            ArrayLayers = createInfo.ArrayLayers,
            BaseArrayLayer = createInfo.BaseArrayLayer,
            BaseMipLevel = createInfo.BaseMipLevel,
            Format = createInfo.FormatOverride.HasValue
                ? VeldridConverter.ConvertPixelFormat(createInfo.FormatOverride.Value)
                : null,
            MipLevels = createInfo.MipLevels,
            Target = texture.texture,
        });
        try
        {
            return new VeldridTextureView(view)
            {
                TargetTexture = createInfo.TargetTexture,
            };
        }
        catch
        {
            view?.Dispose();
            throw;
        }
    }

    internal VeldridResourceFactory(GraphicsDevice device)
    {
        dev = device;
    }
}