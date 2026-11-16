namespace Line.Framework.Graphics;

/// <summary>
/// 资源集里「一个槽位」实际要绑的资源。
/// <para>
/// 用哪种资源由布局里对应元素的 <see cref="ResourceLayoutElement.Kind"/> 决定，
/// 本类型只是把三种可能的载体（纹理 / 采样器 / 缓冲）装在一起，
/// 由后端在 <see cref="IResourceFactory.CreateResourceSet"/> 里按 Kind 取用。
/// </para>
/// <para>
/// <b>槽位顺序 = 布局元素顺序</b>，一对一，不允许多个元素共用一个绑定
/// （<see cref="ResourceKind.TextureSampler"/> 的「纹理 + 采样器」在抽象层已是一个元素）。
/// </para>
/// </summary>
public readonly struct ResourceBinding
{
    /// <summary>TextureSampler 槽位用。</summary>
    public ITexture Texture { get; }

    /// <summary>TextureSampler 槽位用；null = 使用设备默认采样器。</summary>
    public ISampler Sampler { get; }

    /// <summary>UniformBuffer / StorageBuffer 槽位用。</summary>
    public IDeviceBuffer Buffer { get; }

    private ResourceBinding(ITexture texture, ISampler sampler, IDeviceBuffer buffer)
    {
        Texture = texture;
        Sampler = sampler;
        Buffer = buffer;
    }

    /// <summary>创建一个纹理 + 采样器槽位的绑定。</summary>
    /// <param name="texture">纹理，不可为 null。</param>
    /// <param name="sampler">采样器；null 表示使用设备默认采样器（线性 + Clamp）。</param>
    public static ResourceBinding FromTextureSampler(ITexture texture, ISampler sampler = null) =>
        // 传 null 视为没有绑定，而非「绑定 null 纹理」——尽早暴露成参数错误。
        texture is null
            ? throw new ArgumentNullException(nameof(texture))
            : new ResourceBinding(texture, sampler, null);

    /// <summary>创建一个缓冲槽位的绑定（uniform 或 storage）。</summary>
    /// <param name="buffer">缓冲，不可为 null。</param>
    public static ResourceBinding FromBuffer(IDeviceBuffer buffer) =>
        buffer is null
            ? throw new ArgumentNullException(nameof(buffer))
            : new ResourceBinding(null, null, buffer);

    /// <summary>调试用描述，形如 <c>Buffer(SDL3Uniform)</c>。</summary>
    public override string ToString() =>
        Buffer is not null
            ? $"Buffer({Buffer.Name ?? Buffer.GetType().Name})"
            : $"Texture({Texture?.Name ?? Texture?.GetType().Name})";
}
