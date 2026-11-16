namespace Line.Framework.Graphics;

/// <summary>
/// 正面判定方向：按顶点在屏幕空间中的绕序认定正面。
/// <para>
/// 与 <see cref="FaceCullMode"/> 配合使用。注意各图形 API 的默认约定不同，
/// 后端负责换算，调用方只需按自己的顶点数据如实声明。
/// </para>
/// </summary>
public enum FrontFace : byte
{
    /// <summary>顺时针绕序为正面。</summary>
    Clockwise,

    /// <summary>逆时针绕序为正面。</summary>
    CounterClockwise,
}
