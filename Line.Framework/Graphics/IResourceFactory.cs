namespace Line.Framework.Graphics;

public interface IResourceFactory
{
    IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo);
    ITexture CreateTexture(TextureCreateInfo createInfo);
    ISampler CreateSampler(SamplerCreateInfo createInfo);
    IShader CreateShader(ShaderCreateInfo createInfo);
    IFrameBuffer CreateFrameBuffer(FramebufferCreateInfo createInfo);
    IPipeline CreatePipeline(PipelineCreateInfo createInfo);
    ITextureView CreateTextureView(TextureViewCreateInfo createInfo);
}