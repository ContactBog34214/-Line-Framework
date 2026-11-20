namespace Line.Framework.Graphics;

/// <summary>
/// 图元拓扑：顶点如何被组装成图形。
/// <para>
/// 本框架的 UI / 2D 渲染使用 <see cref="TriangleList"/>（每个四边形拆两个三角形）。
/// List 与 Strip 的区别在于顶点是否复用：List 每 N 个顶点独立成一个图元，
/// Strip 则让相邻图元共享顶点，能省带宽但要求顶点按特定顺序排布。
/// </para>
/// </summary>
public enum PrimitiveTopology
{
    /// <summary>每个顶点是一个点。</summary>
    PointList,

    /// <summary>每 2 个顶点一条独立线段。</summary>
    LineList,

    /// <summary>顶点连成折线（每对相邻顶点一条线段，共享端点）。</summary>
    LineStrip,

    /// <summary>每 3 个顶点一个独立三角形（本框架默认）。</summary>
    TriangleList,

    /// <summary>顶点连成三角形带，每 N 个连续顶点构成一个三角形。</summary>
    TriangleStrip,
}
