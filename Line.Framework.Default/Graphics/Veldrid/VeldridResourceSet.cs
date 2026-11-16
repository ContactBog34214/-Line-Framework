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

        var bindings = createInfo.ResolveBindings();
        if (bindings.Length != elements.Length)
            throw new ArgumentException(
                $"Layout declares {elements.Length} element(s) but "
                    + $"{bindings.Length} binding(s) were supplied. "
                    + "Bindings are matched to elements by position.",
                nameof(createInfo)
            );

        // 展开顺序必须与 CreateResourceLayout 完全一致：
        //   TextureSampler → [纹理, 采样器]（先纹理、后采样器）
        //   UniformBuffer  → [缓冲]
        // 写反了不会报错，但采样器会绑到纹理槽上，画面全黑。
        var bound = new List<global::Veldrid.BindableResource>(elements.Length * 2);

        // 必须与 CreateResourceLayout 用完全相同的顺序规则，
        // 否则 BoundResources 与 ResourceLayout 的槽位对不上。
        foreach (var i in ResourceLayoutOrdering.CanonicalOrder(elements))
        {
            var e = elements[i];
            var b = bindings[i];
            var label = string.IsNullOrEmpty(e.Name) ? $"#{i}" : $"'{e.Name}'";

            if (e.Stages == ShaderStage.None)
                throw new ArgumentException(
                    $"Layout element {label} declares no shader stage.",
                    nameof(createInfo)
                );

            switch (e.Kind)
            {
                case ResourceKind.TextureSampler:
                {
                    if (b.Texture is not VeldridTexture texture)
                        throw new ArgumentException(
                            $"Layout element {label} is TextureSampler but the binding is "
                                + $"{b.Texture?.GetType().Name ?? "not a texture"}.",
                            nameof(createInfo)
                        );
                    if ((texture.Usage & TextureUsage.Sampled) == 0)
                        throw new ArgumentException(
                            $"Texture bound to {label} was created with Usage = "
                                + $"{texture.Usage}, which does not include Sampled. It cannot "
                                + "be bound to a TextureSampler slot — pass TextureUsage.Sampled "
                                + "(or Sampled | ColorTarget) when creating it.",
                            nameof(createInfo)
                        );

                    var samplerOwner = b.Sampler ?? DefaultSampler;
                    if (samplerOwner is not VeldridSampler sampler)
                        throw new ArgumentException(
                            $"Sampler bound to {label} is "
                                + $"{samplerOwner?.GetType().Name ?? "null"}, not a VeldridSampler.",
                            nameof(createInfo)
                        );

                    bound.Add(texture.texture);
                    bound.Add(sampler.sampler);
                    break;
                }

                case ResourceKind.UniformBuffer:
                {
                    if (b.Buffer is not VeldridBuffer buffer)
                        throw new ArgumentException(
                            $"Layout element {label} is UniformBuffer but the binding is "
                                + $"{b.Buffer?.GetType().Name ?? "not a buffer"}.",
                            nameof(createInfo)
                        );
                    if ((buffer.Usage & BufferUsage.Uniform) == 0)
                        throw new ArgumentException(
                            $"Buffer bound to {label} was created with Usage = {buffer.Usage}, "
                                + "which does not include Uniform. Veldrid rejects the binding "
                                + "at pipeline/descriptor level — pass BufferUsage.Uniform.",
                            nameof(createInfo)
                        );
                    if (e.SizeInBytes > 0 && buffer.SizeInBytes < e.SizeInBytes)
                        throw new ArgumentException(
                            $"Layout element {label} expects {e.SizeInBytes} bytes but the "
                                + $"bound buffer is {buffer.SizeInBytes} bytes.",
                            nameof(createInfo)
                        );

                    bound.Add(buffer.buffer);
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"Layout element {label} is {e.Kind}; the Veldrid backend currently "
                            + "supports UniformBuffer and TextureSampler only."
                    );
            }
        }

        var set = dev.ResourceFactory.CreateResourceSet(
            new global::Veldrid.ResourceSetDescription(layout.ResourceLayout, [.. bound])
        );
        set.Name = $"ResourceSet/{elements.Length}";

        return new VeldridResourceSet(set, createInfo.Layout);
    }

    /// <summary>释放工厂持有的常驻资源（当前只有默认采样器）。</summary>
    internal void DisposeOwned()
    {
        defaultSampler?.Dispose();
        defaultSampler = null;
    }
}
