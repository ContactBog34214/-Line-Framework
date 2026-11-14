using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 资源集合：按某个布局绑定好的一组实际资源。
/// </summary>
public sealed class VeldridResourceSet : IResourceSet
{
    internal readonly global::Veldrid.ResourceSet ResourceSet;

    /// <summary>创建时使用的布局（抽象层视角）。</summary>
    public IResourceLayout Layout { get; }

    public string Name => ResourceSet?.Name ?? string.Empty;

    internal VeldridResourceSet(global::Veldrid.ResourceSet set, IResourceLayout layout)
    {
        ResourceSet = set;
        Layout = layout;
    }

    public void Dispose()
    {
        ResourceSet?.Dispose();
    }
}

public sealed partial class VeldridResourceFactory
{
    private ISampler defaultSampler;

    /// <summary>
    /// 设备默认采样器：线性过滤 + Clamp 寻址。
    /// <para>
    /// Clamp 而非 Wrap —— UI 图片一旦 Wrap，边缘会渗出对侧像素。
    /// 按需创建一次后常驻，随工厂一起释放。
    /// </para>
    /// </summary>
    internal ISampler DefaultSampler =>
        defaultSampler ??= CreateSampler(
            new SamplerCreateInfo
            {
                MinFilter = SampleFilter.Linear,
                MagFilter = SampleFilter.Linear,
                MipmapMode = MipmapMode.Linear,
                AddressU = SamplerAddressMode.Clamp,
                AddressV = SamplerAddressMode.Clamp,
                AddressW = SamplerAddressMode.Clamp,
                // UI 纹理通常不带 mipmap，限制在 mip 0 避免无谓采样
                MinLod = 0,
                MaxLod = 0,
                AnisotropyEnabled = false,
            }
        );

    public IResourceSet CreateResourceSet(ResourceSetCreateInfo createInfo)
    {
        if (createInfo.Layout is null)
            throw new ArgumentException(
                "ResourceSetCreateInfo.Layout is null.",
                nameof(createInfo)
            );
        if (createInfo.Layout is not VeldridResourceLayout layout)
            throw new ArgumentException(
                $"Layout {createInfo.Layout.GetType().Name} is not a VeldridResourceLayout.",
                nameof(createInfo)
            );

        var elements = layout.Elements;
        if (elements.Length == 0)
            throw new ArgumentException(
                "Layout declares no elements; a resource set bound to it would be empty.",
                nameof(createInfo)
            );

        // 当前只支持单个元素、且必须是 TextureSampler。
        // 多元素布局等真有需求时再改成按 elements 逐项装配。
        if (elements.Length != 1)
            throw new NotSupportedException(
                $"Layout has {elements.Length} elements; only single-element layouts are "
                    + "supported by this overload. Extend CreateResourceSet before using it."
            );

        var kind = elements[0].Kind;
        if (kind != ResourceKind.TextureSampler)
            throw new NotSupportedException(
                $"Layout element '{elements[0].Name}' is {kind}; "
                    + "only TextureSampler is currently supported."
            );

        var texture = createInfo.Texture;
        if (texture is null)
            throw new ArgumentException(
                $"Layout element '{elements[0].Name}' is a TextureSampler but "
                    + "ResourceSetCreateInfo.Texture is null.",
                nameof(createInfo)
            );
        if (texture is not VeldridTexture vtex)
            throw new ArgumentException(
                $"Texture {texture.GetType().Name} is not a VeldridTexture.",
                nameof(createInfo)
            );
        if ((texture.Usage & TextureUsage.Sampled) == 0)
            throw new ArgumentException(
                $"Texture was created with Usage = {texture.Usage}, which does not include "
                    + "Sampled. It cannot be bound to a TextureSampler slot — pass "
                    + "TextureUsage.Sampled (or Sampled | ColorTarget) when creating it.",
                nameof(createInfo)
            );

        var samplerOwner = createInfo.Sampler ?? DefaultSampler;
        if (samplerOwner is not VeldridSampler vsampler)
            throw new ArgumentException(
                $"Sampler {samplerOwner.GetType().Name} is not a VeldridSampler.",
                nameof(createInfo)
            );

        // 顺序必须与 CreateResourceLayout 的展开顺序一致：先纹理、后采样器。
        // 写反了不会报错，但采样器会绑到纹理槽上，画面全黑。
        global::Veldrid.BindableResource[] bound = [vtex.texture, vsampler.sampler];

        var set = dev.ResourceFactory.CreateResourceSet(
            new global::Veldrid.ResourceSetDescription(layout.ResourceLayout, bound)
        );
        set.Name = $"ResourceSet/{elements[0].Name}";

        return new VeldridResourceSet(set, createInfo.Layout);
    }

    /// <summary>释放工厂持有的常驻资源（当前只有默认采样器）。</summary>
    internal void DisposeOwned()
    {
        defaultSampler?.Dispose();
        defaultSampler = null;
    }
}
