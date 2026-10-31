using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 资源布局。
/// <para>
/// 注意：底层 <see cref="global::Veldrid.ResourceLayout"/> 是「展开后」的产物 ——
/// 一个 <see cref="ResourceKind.TextureSampler"/> 会在其中展开成
/// TextureReadOnly + Sampler 两个槽位；
/// 而 <see cref="Elements"/> 保存的是抽象层视角（未展开）的元素。
/// 两者数量并不相同，做任何计数对比时请以 <see cref="Elements"/> 为准。
/// </para>
/// </summary>
public sealed class VeldridResourceLayout : IResourceLayout
{
    private readonly ResourceLayoutElement[] elements;

    internal global::Veldrid.ResourceLayout ResourceLayout { get; }

    /// <summary>调试标签。不参与任何资源匹配。</summary>
    public string Name => ResourceLayout?.Name ?? string.Empty;

    /// <summary>
    /// 抽象层元素（未展开）。binding 编号规则：按 Kind 各自独立编号，
    /// 同类内部按出现顺序从 0 递增（对齐 Veldrid.ResourceBindingModel.Default）。
    /// </summary>
    public ReadOnlySpan<ResourceLayoutElement> Elements => elements;

    internal VeldridResourceLayout(
        global::Veldrid.ResourceLayout resourceLayout,
        ResourceLayoutElement[] snapshot
    )
    {
        ResourceLayout = resourceLayout;
        elements = snapshot;
    }

    public void Dispose()
    {
        ResourceLayout?.Dispose();
    }
}

public sealed partial class VeldridResourceFactory
{
    public IResourceLayout CreateResourceLayout(ResourceLayoutCreateInfo createInfo)
    {
        var source = createInfo.Elements;
        if (source is not { Length: > 0 })
            throw new ArgumentException(
                "Resource layout must declare at least one element.",
                nameof(createInfo)
            );

        // CreateInfo 是 struct、Elements 是数组引用：这里做一次快照，
        // 保证「已创建的 GPU 布局」与「IResourceLayout.Elements 报告的内容」永远一致，
        // 不会因为调用方后续改写数组而漂移。
        var snapshot = new ResourceLayoutElement[source.Length];
        Array.Copy(source, snapshot, source.Length);

        var elements = new List<global::Veldrid.ResourceLayoutElementDescription>(
            snapshot.Length * 2
        );

        for (int i = 0; i < snapshot.Length; i++)
        {
            var e = snapshot[i];

            if (e.Stages == ShaderStage.None)
                throw new ArgumentException(
                    $"Resource layout element #{i} ('{e.Name}') declares no shader stage.",
                    nameof(createInfo)
                );

            var stages = VeldridConverter.ConvertShaderStages(e.Stages);
            var name = string.IsNullOrEmpty(e.Name) ? $"res{i}" : e.Name;

            if (e.Kind == ResourceKind.TextureSampler)
            {
                // 一个 TextureSampler 展开成「纹理 + 采样器」两个槽位。
                // 顺序固定为「先纹理、后采样器」；
                // CreateResourceSet 的 BoundResources 必须按完全相同的顺序给出。
                elements.Add(new(name, global::Veldrid.ResourceKind.TextureReadOnly, stages));
                elements.Add(new(name, global::Veldrid.ResourceKind.Sampler, stages));
            }
            else
            {
                elements.Add(VeldridConverter.ConvertResourceLayoutElement(e, (uint)i));
            }
        }

        var layout = dev.ResourceFactory.CreateResourceLayout(
            new global::Veldrid.ResourceLayoutDescription(elements.ToArray())
        );
        layout.Name = $"ResourceLayout/{snapshot.Length}";

        return new VeldridResourceLayout(layout, snapshot);
    }
}

public static partial class VeldridConverter
{
    /// <summary>
    /// 抽象层资源种类 → Veldrid 资源种类。仅支持 1:1 的映射。
    /// <see cref="ResourceKind.TextureSampler"/> 是 1 → 2 展开，无法用单值表达，
    /// 会抛 <see cref="NotSupportedException"/>；
    /// 请在 <see cref="VeldridResourceFactory.CreateResourceLayout"/> 中成对处理。
    /// </summary>
    public static global::Veldrid.ResourceKind ConvertResourceKind(ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.UniformBuffer => global::Veldrid.ResourceKind.UniformBuffer,
            ResourceKind.StorageBufferReadOnly => global::Veldrid
                .ResourceKind
                .StructuredBufferReadOnly,
            ResourceKind.StorageBufferReadWrite => global::Veldrid
                .ResourceKind
                .StructuredBufferReadWrite,
            // 可写纹理（GLSL image2D / D3D UAV），不是 TextureReadOnly。
            ResourceKind.StorageTexture => global::Veldrid.ResourceKind.TextureReadWrite,
            ResourceKind.TextureSampler => throw new NotSupportedException(
                "TextureSampler expands into a texture + sampler pair; "
                    + "handle it in VeldridResourceFactory.CreateResourceLayout."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// 单个布局元素 → Veldrid 元素描述。仅适用于 1:1 的种类，
    /// <see cref="ResourceKind.TextureSampler"/> 会由 <see cref="ConvertResourceKind"/> 抛出。
    /// </summary>
    public static global::Veldrid.ResourceLayoutElementDescription ConvertResourceLayoutElement(
        ResourceLayoutElement element,
        uint index
    )
    {
        if (element.Stages == ShaderStage.None)
            throw new ArgumentException(
                $"Resource layout element #{index} declares no shader stage.",
                nameof(element)
            );

        return new()
        {
            Name = string.IsNullOrEmpty(element.Name) ? $"res{index}" : element.Name,
            Kind = ConvertResourceKind(element.Kind),
            Stages = ConvertShaderStages(element.Stages),
        };
    }
}
