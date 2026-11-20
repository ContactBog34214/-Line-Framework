namespace Line.Framework.Graphics;

/// <summary>
/// 创建纹理视图的参数：只取源纹理的某个 mip / 层区间。
/// <para>
/// <b>SDL3 后端会忽略这些区间字段</b>（该后端没有视图概念，直接使用源纹理），
/// 因此需要跨后端一致时，不要依赖视图做局部访问。
/// </para>
/// </summary>
public struct TextureViewCreateInfo
{
    /// <summary>源纹理。视图不拥有它，释放顺序应为「先视图后纹理」。</summary>
    public ITexture TargetTexture { get; set; }

    /// <summary>起始 mip 层（含）。</summary>
    public uint BaseMipLevel { get; set; }

    /// <summary>可见的 mip 层数量。</summary>
    public uint MipLevels { get; set; }

    /// <summary>起始数组层（含）。</summary>
    public uint BaseArrayLayer { get; set; }

    /// <summary>可见的数组层数量。</summary>
    public uint ArrayLayers { get; set; }

    /// <summary>
    /// 格式覆盖；非 null 时以该格式解释纹理数据（用于 sRGB / 线性切换等）。
    /// SDL3 后端不支持。
    /// </summary>
    public PixelFormat? FormatOverride { get; set; }
}
