namespace Line.Framework.Graphics
{
    /// <summary>
    /// 混合因子：源 / 目标颜色在混合方程中的系数。
    /// <para>
    /// 最终颜色 = <c>源 × 源因子  BlendOp  目标 × 目标因子</c>。
    /// 名称里的 Source 指本次绘制的像素，Destination 指已存在于目标上的像素。
    /// </para>
    /// <para>
    /// 最常用的是 <see cref="SourceAlpha"/> + <see cref="InverseSourceAlpha"/>，
    /// 即标准 alpha 混合；<see cref="BlendStateDescription"/> 的预置里已封装。
    /// </para>
    /// </summary>
    public enum BlendFactor
    {
        /// <summary>全零：该项不参与混合。</summary>
        Zero,

        /// <summary>全一：该项原样保留。</summary>
        One,

        /// <summary>源颜色。</summary>
        SourceColor,

        /// <summary>源颜色的反色（1 − 源颜色）。</summary>
        InverseSourceColor,

        /// <summary>源 alpha。</summary>
        SourceAlpha,

        /// <summary>源 alpha 的反值（1 − 源 alpha）。</summary>
        InverseSourceAlpha,

        /// <summary>目标颜色。</summary>
        DestinationColor,

        /// <summary>目标颜色的反色（1 − 目标颜色）。</summary>
        InverseDestinationColor,

        /// <summary>目标 alpha。</summary>
        DestinationAlpha,

        /// <summary>目标 alpha 的反值（1 − 目标 alpha）。</summary>
        InverseDestinationAlpha,

        /// <summary>
        /// 混合常量（由管线状态外部提供）。
        /// 抽象层目前没有暴露该常量，使用时需注意。
        /// </summary>
        BlendConstant,

        /// <summary>混合常量的反值。</summary>
        InverseBlendConstant,

        /// <summary>
        /// <c>min(源 alpha, 1 − 目标 alpha)</c>。
        /// 用于双面透明等需要抑制自身重复叠加的场景。
        /// </summary>
        SourceAlphaSaturate,
    }
}
