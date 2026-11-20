namespace Line.Framework.Graphics;

/// <summary>
/// mipmap 层之间的过滤方式。
/// <para>
/// 只影响「不同 mip 层之间怎么插值」，同一层内的放大缩小由
/// <see cref="SampleFilter"/>（Min/Mag 过滤）决定。
/// UI 纹理通常不带 mipmap，这项无实际影响。
/// </para>
/// </summary>
public enum MipmapMode
{
    /// <summary>取最近的 mip 层，层间不插值（远处会有明显跳变）。</summary>
    Nearest,

    /// <summary>相邻两层按比例线性插值（三线性过滤的后半段）。</summary>
    Linear,
}
