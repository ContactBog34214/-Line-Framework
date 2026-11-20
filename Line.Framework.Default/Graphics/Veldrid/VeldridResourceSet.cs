using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 资源集合：按某个布局绑定好的一组实际资源。
/// </summary>
public sealed class VeldridResourceSet : IResourceSet
{
    internal readonly global::Veldrid.ResourceSet ResourceSet;

    /// <summary>
    /// 为绑定而临时创建的纹理视图。
    /// <para>
    /// <b>为什么需要它们</b>：直接把 <c>Veldrid.Texture</c> 绑进资源集时，
    /// Veldrid 内部会自建一个<b>只覆盖 mip 0</b> 的视图 —— 于是采样器
    /// 无论 LOD 算成多少都只能读到最细的一层，mipmap 完全失效且不报错。
    /// 必须显式创建覆盖全部 mip 范围的 <c>TextureView</c> 才能采到 mip 链。
    /// </para>
    /// <para>
    /// 生命周期跟随资源集：先释放视图，再释放资源集和纹理。
    /// </para>
    /// </summary>
    private readonly global::Veldrid.TextureView[] views;

    /// <summary>创建时使用的布局（抽象层视角）。</summary>
    public IResourceLayout Layout { get; }

    public string Name => ResourceSet?.Name ?? string.Empty;

    internal VeldridResourceSet(
        global::Veldrid.ResourceSet set,
        IResourceLayout layout,
        global::Veldrid.TextureView[] views = null
    )
    {
        ResourceSet = set;
        Layout = layout;
        this.views = views ?? [];
    }

    public void Dispose()
    {
        // 视图引用纹理，必须先于资源集与纹理释放。
        foreach (var v in views)
            v?.Dispose();

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
                // Veldrid 后端的 mipmap 采样不可用（见 VeldridCapabilities.SupportsMipmap），
                // 资源加载器因此不会生成 mip 链，纹理只有 1 层。
                // 采样器限制在 mip0 是诚实的表达，也避免 LOD 落到不存在的层。
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

        // 为每个 TextureSampler 槽位创建覆盖全部 mip 的视图，
        // 与资源集同生命周期（Dispose 时先释放视图）。
        var views = new List<global::Veldrid.TextureView>();

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

                    // 必须绑「覆盖全部 mip」的视图，不能绑裸纹理 ——
                    // 裸纹理会让 Veldrid 自建一个只有 mip0 的视图，
                    // 采样器永远读不到 mip 链（不报错，只是 mipmap 静默失效）。
                    //
                    // mipLevels = texture.MipLevels（0 在这里表示「到末尾」，
                    // 但显式传实际层数更直观，也避免依赖那个约定）。
                    var view = CreateFullMipView(texture);
                    views.Add(view);

                    bound.Add(view);
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

        try
        {
            var set = dev.ResourceFactory.CreateResourceSet(
                new global::Veldrid.ResourceSetDescription(layout.ResourceLayout, [.. bound])
            );
            set.Name = $"ResourceSet/{elements.Length}";

            return new VeldridResourceSet(set, createInfo.Layout, [.. views]);
        }
        catch
        {
            // 创建失败时视图会泄漏 —— 它们不被任何对象持有。
            foreach (var v in views)
                v?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 创建一个覆盖纹理<b>全部 mip 与数组层</b>的视图。
    /// <para>
    /// 资源集直接绑 <c>Veldrid.Texture</c> 时，Veldrid 会自建一个默认视图。
    /// 显式给出完整范围更可控，也避免将来引入多层纹理时踩到范围不一致。
    /// （注：即使视图正确，本后端的 mipmap 采样仍然不可用 ——
    /// 见 <see cref="VeldridCapabilities.SupportsMipmap"/>。）
    /// </para>
    /// </summary>
    private global::Veldrid.TextureView CreateFullMipView(VeldridTexture texture)
    {
        var target = texture.texture;

        // 显式给出 (baseMip, mipLevels, baseLayer, arrayLayers)：
        // 单参构造器的默认值不够明确，而这里必须确保 mipLevels 覆盖整条链。
        // 即使本后端 mipmap 采样不可用，纹理若有多层（离屏/未来的用途）
        // 视图也必须完整覆盖，否则算作绑定错误。
        return dev.ResourceFactory.CreateTextureView(
            new global::Veldrid.TextureViewDescription(
                target,
                0, // baseMipLevel
                target.MipLevels, // mipLevels：全部可见
                0, // baseArrayLayer
                target.ArrayLayers // arrayLayers：全部可见
            )
        );
    }

    /// <summary>释放工厂持有的常驻资源（当前只有默认采样器）。</summary>
    internal void DisposeOwned()
    {
        defaultSampler?.Dispose();
        defaultSampler = null;
    }
}
