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
    IResourceLayout CreateResourceLayout(ResourceLayoutCreateInfo createInfo);

    /// <summary>
    /// 按布局把实际资源装成一个可绑定的集合。
    /// <para>
    /// 创建昂贵（Vulkan 上会从 descriptor pool 分配），应长期复用以避免每帧重建。
    /// 生命周期跟随它引用的纹理：先 Dispose 本对象，再 Dispose 纹理。
    /// </para>
    /// <para>
    /// <see cref="ResourceSetCreateInfo.Sampler"/> 为 null 时使用设备默认采样器
    /// （线性过滤 + Clamp 寻址）。
    /// </para>
    /// </summary>
    IResourceSet CreateResourceSet(ResourceSetCreateInfo createInfo);

    ICommandList CreateCommandList(string Name = null);
}
