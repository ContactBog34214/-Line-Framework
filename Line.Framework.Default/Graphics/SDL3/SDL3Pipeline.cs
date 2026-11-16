using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 资源集。
/// <para>
/// SDL3 没有 descriptor set，也没有 <c>SDL_BindGPU*UniformBuffers</c>：
/// <list type="bullet">
/// <item>纹理 + 采样器 → <c>SDL_BindGPU{Vertex,Fragment}Samplers</c>，按阶段分两组</item>
/// <item>uniform → <c>SDL_PushGPU{Vertex,Fragment}UniformData</c>，
///       在<b>每次绑定资源集时</b>把 CPU 侧的字节推送进命令缓冲</item>
/// </list>
/// 因此本类同时充当「绑定列表」和「推送源」，由
/// <see cref="SDL3CommandList.SetGraphicsResourceSet"/> 消费。
/// </para>
/// <para>
/// <b>与 Veldrid 的语义差别</b>：SDL3 的 uniform 是「推送即快照」——
/// 推送之后再改 <see cref="SDL3UniformBuffer"/> 的内容不会影响已经推送过的绘制。
/// Veldrid 是「绑定即引用」，改缓冲内容会影响后续绘制。
/// 只要保持「先 Update 再 SetGraphicsResourceSet」的顺序，两者表现一致。
/// </para>
/// </summary>
public sealed class SDL3ResourceSet : IResourceSet
{
    private int disposed;

    public IResourceLayout Layout { get; }
    public string Name { get; internal set; } = "SDL3ResourceSet";

    internal GPUTextureSamplerBinding[] VertexSamplers { get; }
    internal GPUTextureSamplerBinding[] FragmentSamplers { get; }

    /// <summary>按声明顺序排列，槽位从 SetGraphicsResourceSet 的 slot 起递增。</summary>
    internal SDL3UniformBuffer[] VertexUniforms { get; }
    internal SDL3UniformBuffer[] FragmentUniforms { get; }

    internal SDL3ResourceSet(
        IResourceLayout layout,
        GPUTextureSamplerBinding[] vertexSamplers,
        GPUTextureSamplerBinding[] fragmentSamplers,
        SDL3UniformBuffer[] vertexUniforms,
        SDL3UniformBuffer[] fragmentUniforms
    )
    {
        Layout = layout;
        VertexSamplers = vertexSamplers;
        FragmentSamplers = fragmentSamplers;
        VertexUniforms = vertexUniforms;
        FragmentUniforms = fragmentUniforms;
    }

    public void Dispose()
    {
        // 资源集本身不拥有纹理/采样器/缓冲，无需释放。
        Interlocked.Exchange(ref disposed, 1);
    }
}

/// <summary>
/// SDL3 资源布局。
/// <para>
/// SDL3 没有布局概念 —— 资源数量是在创建 <b>着色器</b> 时以计数的形式声明的。
/// 因此本类只保存抽象层的元素列表，供创建着色器时统计数量使用。
/// </para>
/// </summary>
public sealed class SDL3ResourceLayout : IResourceLayout
{
    private readonly ResourceLayoutElement[] elements;

    public string Name { get; internal set; } = "SDL3ResourceLayout";

    public ReadOnlySpan<ResourceLayoutElement> Elements => elements;

    internal SDL3ResourceLayout(ResourceLayoutElement[] snapshot)
    {
        elements = snapshot;
    }

    /// <summary>统计某类资源的个数，供 <c>GPUShaderCreateInfo</c> 的计数使用。</summary>
    internal uint CountOf(ResourceKind kind)
    {
        uint n = 0;
        foreach (var e in elements)
            if (e.Kind == kind)
                n++;
        return n;
    }

    public void Dispose()
    {
        // 无底层资源
    }
}

/// <summary>
/// SDL3 图形管线。
/// </summary>
public sealed class SDL3Pipeline : IPipeline
{
    private readonly IntPtr dev;
    internal IntPtr Handle;
    private int disposed;

    public PipelineType Type { get; }
    public OutputFormatDescription Outputs { get; }
    public IResourceLayout[] ResourceLayouts { get; }
    public string Name { get; internal set; } = "SDL3Pipeline";

    internal SDL3Pipeline(
        IntPtr device,
        IntPtr handle,
        OutputFormatDescription outputs,
        IResourceLayout[] layouts
    )
    {
        dev = device;
        Handle = handle;
        Type = PipelineType.Graphics;
        Outputs = outputs;
        ResourceLayouts = layouts ?? [];
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (Handle != IntPtr.Zero)
        {
            SDL.ReleaseGPUGraphicsPipeline(dev, Handle);
            Handle = IntPtr.Zero;
        }
    }
}
