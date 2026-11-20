namespace Line.Framework.Graphics;

/// <summary>
/// 混合运算：把源因子与目标因子组合起来的方式。
/// <para>
/// 结果 = <c>源 × 源因子  &lt;本运算&gt;  目标 × 目标因子</c>。
/// 绝大多数 2D / UI 绘制只需要 <see cref="Add"/>。
/// </para>
/// </summary>
public enum BlendOperation
{
    /// <summary>相加（标准混合）。</summary>
    Add,

    /// <summary>源减目标。</summary>
    Subtract,

    /// <summary>目标减源。</summary>
    ReverseSubtract,

    /// <summary>取分量较小者。</summary>
    Min,

    /// <summary>取分量较大者。</summary>
    Max,
}
