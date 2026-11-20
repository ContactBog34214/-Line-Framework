namespace Line.Framework.Graphics;

/// <summary>
/// 顶点属性的更新频率：每顶点一次，还是每实例一次。
/// <para>
/// 注意类型名是 <c>VertexInputRate</c>（文件名沿用早期命名）。
/// 实例化绘制（一次 Draw 画多个相同网格）时，实例属性用 <see cref="Instance"/>，
/// 配合 <see cref="VertexLayoutDescription"/> 中各 buffer 的独立步长。
/// </para>
/// </summary>
public enum VertexInputRate
{
    /// <summary>每个顶点前进一次（默认）。</summary>
    Vertex,

    /// <summary>每个实例前进一次，同一实例内所有顶点共用。</summary>
    Instance,
}
