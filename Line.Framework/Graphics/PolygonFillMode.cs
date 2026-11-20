namespace Line.Framework.Graphics;

/// <summary>
/// 三角形填充模式。
/// <para>
/// <see cref="Wireframe"/> 只画边，常用于调试网格与投影是否正确；
/// 部分后端（如 SDL3 的 GPU 后端）不暴露该开关，实际会被忽略。
/// </para>
/// </summary>
public enum PolygonFillMode : byte
{
    /// <summary>实心填充（正常渲染）。</summary>
    Solid,

    /// <summary>只画三角形边线（调试用）。</summary>
    Wireframe,
}
