namespace Line.Framework.Graphics;

/// <summary>
/// 背面剔除模式。
/// <para>
/// 「正面」由 <see cref="FrontFace"/> 与顶点绕序共同决定。
/// 2D / UI 绘制通常用 <see cref="None"/>，因为四边形顶点的绕序不一定统一；
/// 3D 实体用 <see cref="Back"/> 可以省掉一半填充率。
/// </para>
/// </summary>
public enum FaceCullMode : byte
{
    /// <summary>剔除背面（默认的 3D 选择）。</summary>
    Back,

    /// <summary>剔除正面，保留背面（用于从内部观察封闭体）。</summary>
    Front,

    /// <summary>不剔除，双面都画。</summary>
    None,
}
