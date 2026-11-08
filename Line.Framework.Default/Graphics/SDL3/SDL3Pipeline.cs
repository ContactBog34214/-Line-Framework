using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 资源集。
/// <para>
/// SDL3 没有 descriptor set：绑定是「纹理 + 采样器」成对直接绑在渲染通道上的。
/// 因此本类只是一个待绑定的配对列表，由
/// <see cref="SDL3CommandList.SetGraphicsResourceSet"/> 消费。
/// </para>
/// </summary>
public sealed class SDL3ResourceSet : IResourceSet
{
    private int disposed;

    public IResourceLayout Layout { get; }
    public string Name { get; internal set; } = "SDL3ResourceSet";

    internal GPUTextureSamplerBinding[] Bindings { get; }

    internal SDL3ResourceSet(IResourceLayout layout, GPUTextureSamplerBinding[] bindings)
    {
        Layout = layout;
        Bindings = bindings;
    }

    public void Dispose()
    {
        // 资源集本身不拥有纹理/采样器，无需释放。
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
