namespace Line.Framework.Graphics
{
    /// <summary>
    /// 颜色写入掩码：控制混合结果能写进哪些通道。
    /// <para>
    /// 与「混合」是两个不同阶段 —— 混合决定算出的颜色，掩码决定写不写。
    /// 例：只写 alpha 而不动 RGB 时用 <see cref="Alpha"/>。
    /// </para>
    /// </summary>
    [Flags]
    public enum ColorWriteMask
    {
        /// <summary>禁止写入任何通道（只影响深度 / 模板的 pass 用）。</summary>
        None = 0,

        /// <summary>红通道。</summary>
        Red = 1 << 0,

        /// <summary>绿通道。</summary>
        Green = 1 << 1,

        /// <summary>蓝通道。</summary>
        Blue = 1 << 2,

        /// <summary>Alpha 通道。</summary>
        Alpha = 1 << 3,

        /// <summary>四个通道全写（默认）。</summary>
        All = Red | Green | Blue | Alpha,
    }
}
