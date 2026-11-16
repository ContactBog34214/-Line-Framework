namespace Line.Framework.Graphics;

/// <summary>
/// 创建采样器的参数。
/// <para>
/// <b>UI 渲染的推荐配置</b>：Min/Mag 都用 <see cref="SampleFilter.Linear"/>，
/// 三个方向的寻址都用 <see cref="SamplerAddressMode.Clamp"/>，
/// 并把 <see cref="MaxLod"/> 设为 0（无 mipmap 的纹理避免无谓采样）。
/// 不传采样器时后端默认就是这个配置。
/// </para>
/// </summary>
public struct SamplerCreateInfo
{
    /// <summary>缩小（minify）时的过滤方式。</summary>
    public SampleFilter MinFilter { get; set; }

    /// <summary>放大（magnify）时的过滤方式。</summary>
    public SampleFilter MagFilter { get; set; }

    /// <summary>mip 层之间的过滤方式。</summary>
    public MipmapMode MipmapMode { get; set; }

    /// <summary>U 方向的寻址方式。</summary>
    public SamplerAddressMode AddressU { get; set; }

    /// <summary>V 方向的寻址方式。</summary>
    public SamplerAddressMode AddressV { get; set; }

    /// <summary>W 方向的寻址方式（三维纹理用）。</summary>
    public SamplerAddressMode AddressW { get; set; }

    /// <summary>LOD 偏移，正值使采样偏向更模糊的 mip 层。</summary>
    public int LodBias { get; set; }

    /// <summary>可见的最小 mip 层。</summary>
    public uint MinLod { get; set; }

    /// <summary>可见的最大 mip 层。</summary>
    public uint MaxLod { get; set; }

    /// <summary>是否启用各向异性过滤。</summary>
    public bool AnisotropyEnabled { get; set; }

    /// <summary>各向异性级别上限（仅在启用时有效）。</summary>
    public uint MaxAnisotropy { get; set; }

    /// <summary>
    /// 比较函数；非 null 时这个采样器变成「比较采样器」
    /// （阴影贴图用的 PCF 采样）。普通纹理采样应保持 null。
    /// </summary>
    public CompareFunction? CompareFunction { get; set; }
}
