using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 纹理视图：只取某张纹理的一部分（mip 区间或数组层区间）来使用。
/// <para>
/// <b>后端差异</b>：Veldrid 有真正的 view 对象；SDL3 没有这个概念，
/// 会把目标纹理原样包装（所有区间字段都退化为全范围）。
/// 因此需要跨后端一致时，不要依赖视图做局部访问。
/// </para>
/// <para>
/// 视图不拥有纹理：先释放视图，再释放纹理。
/// </para>
/// </summary>
public interface ITextureView : IDisposable, IName
{
    /// <summary>被引用的源纹理（视图不拥有它）。</summary>
    ITexture TargetTexture { get; }

    /// <summary>起始 mip 层（含）。</summary>
    uint BaseMipLevel { get; }

    /// <summary>可见的 mip 层数量。</summary>
    uint MipLevels { get; }

    /// <summary>起始数组层（含）。</summary>
    uint BaseArrayLayer { get; }

    /// <summary>可见的数组层数量。</summary>
    uint ArrayLayers { get; }
}
