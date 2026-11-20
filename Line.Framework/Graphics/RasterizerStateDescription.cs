namespace Line.Framework.Graphics;

/// <summary>
/// 光栅化状态：剔除、填充模式、绕序与裁剪开关。
/// <para>
/// 2D / UI 渲染一般设置 <c>CullMode = None</c>、<c>FillMode = Solid</c>，
/// 因为四边形顶点的绕序不一定统一，剔除会误删半张图。
/// </para>
/// </summary>
public struct RasterizerStateDescription
{
    /// <summary>剔除哪一面。</summary>
    public FaceCullMode CullMode { get; set; }

    /// <summary>填充模式（实心 / 线框）。</summary>
    public PolygonFillMode FillMode { get; set; }

    /// <summary>正面判定所用的绕序。</summary>
    public FrontFace FrontFace { get; set; }

    /// <summary>
    /// 深度裁剪：是否把 NDC 深度超出 [0,1] 的部分裁掉。
    /// 关闭后越界几何仍会被绘制（可能产生穿插伪影）。
    /// </summary>
    public bool DepthClipEnabled { get; set; }

    /// <summary>
    /// 是否启用裁剪矩形测试。<b>必须为 true</b>，
    /// 否则 <see cref="ICommandList.SetScissorRect"/> 不产生效果。
    /// </summary>
    public bool ScissorTestEnabled { get; set; }
}
