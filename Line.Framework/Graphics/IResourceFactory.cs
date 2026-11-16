namespace Line.Framework.Graphics;

/// <summary>
/// 资源工厂：所有 GPU 资源的创建入口。
/// <para>
/// 从 <see cref="IGraphicsDevice.ResourceFactory"/> 取得，生命周期跟随设备。
/// 传入的 CreateInfo 都是与后端无关的抽象描述，由各后端自行转换。
/// </para>
/// </summary>
public interface IResourceFactory
{
    /// <summary>创建缓冲。<see cref="BufferCreateInfo.SizeInBytes"/> 必须显式赋值。</summary>
    IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo);

    /// <summary>创建纹理。<see cref="TextureCreateInfo.Usage"/> 决定后续可用性。</summary>
    ITexture CreateTexture(TextureCreateInfo createInfo);

    /// <summary>创建采样器。不传时资源集会使用设备默认采样器。</summary>
    ISampler CreateSampler(SamplerCreateInfo createInfo);

    /// <summary>
    /// 创建着色器。
    /// <para>
    /// <see cref="ShaderCreateInfo.Bytes"/> 可以是 GLSL 源码或 SPIR-V 字节，
    /// 两者都会被转换成后端所需的形式。
    /// </para>
    /// <para>
    /// <b>SDL3 特有要求</b>：着色器需要知道自身使用了几个 sampler / uniform /
    /// storage 资源，这些数量来自 <see cref="ShaderCreateInfo.ResourceLayout"/>。
    /// 漏传会让 SDL3 把数量记成 0，导致资源读取失败（且不报错）。
    /// </para>
    /// </summary>
    IShader CreateShader(ShaderCreateInfo createInfo);

    /// <summary>
    /// 创建离屏帧缓冲。
    /// <b>SDL3 后端不支持</b>（它会抛 <see cref="NotSupportedException"/>）——
    /// 该后端没有帧缓冲对象，渲染目标就是纹理本身。
    /// </summary>
    IFrameBuffer CreateFrameBuffer(FramebufferCreateInfo createInfo);

    /// <summary>创建图形管线。交换链的帧缓冲存在时才能取到有效的输出格式。</summary>
    IPipeline CreatePipeline(PipelineCreateInfo createInfo);

    /// <summary>创建纹理视图。SDL3 下退化为原样包装（没有真正的视图概念）。</summary>
    ITextureView CreateTextureView(TextureViewCreateInfo createInfo);

    /// <summary>
    /// 创建资源布局。槽位编号规则见 <see cref="ResourceLayoutCreateInfo"/>。
    /// </summary>
    IResourceLayout CreateResourceLayout(ResourceLayoutCreateInfo createInfo);

    /// <summary>
    /// 按布局把实际资源装成一个可绑定的集合。
    /// <para>
    /// 创建昂贵（Vulkan 上会从 descriptor pool 分配），应长期复用以避免每帧重建。
    /// 生命周期跟随它引用的纹理 / 缓冲：先 Dispose 本对象，再 Dispose 它们。
    /// </para>
    /// <para>
    /// <see cref="ResourceSetCreateInfo.Sampler"/> 为 null 时使用设备默认采样器
    /// （线性过滤 + Clamp 寻址）。
    /// </para>
    /// </summary>
    IResourceSet CreateResourceSet(ResourceSetCreateInfo createInfo);

    /// <summary>创建命令列表。建议每次录制新建一个，不要跨帧复用。</summary>
    ICommandList CreateCommandList(string Name = null);

    /// <summary>
    /// 创建交换链。
    /// <para>
    /// <see cref="SwapchainCreateInfo.Source"/> 的语义随后端不同：
    /// SDL3 期望它携带 <c>SDL_Window*</c>；Veldrid 期望原生窗口句柄
    /// （X11 要 Window + Display*，Wayland 要 wl_surface* + wl_display*）。
    /// </para>
    /// <para>
    /// <see cref="SwapchainCreateInfo.Width"/>/<see cref="SwapchainCreateInfo.Height"/>
    /// 必须是<b>像素</b>尺寸，不是逻辑尺寸。
    /// </para>
    /// </summary>
    ISwapchain CreateSwapchain(SwapchainCreateInfo createInfo);
}
