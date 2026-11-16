namespace Line.Framework.Graphics;

/// <summary>
/// 着色器集合 + 顶点输入布局，构成管线的可编程部分。
/// <para>
/// <see cref="Shaders"/> 里的顺序不重要（后端按 <see cref="IShader.Stage"/> 归位），
/// 但必须同时包含顶点与片元两个阶段，否则管线创建会失败。
/// </para>
/// </summary>
public struct ShaderSetDescription
{
    /// <summary>
    /// 顶点缓冲布局，下标即 <see cref="ICommandList.SetVertexBuffer"/> 的槽位。
    /// 不使用顶点缓冲时传空数组而不是 null。
    /// </summary>
    public VertexLayoutDescription[] VertexLayouts { get; set; }

    /// <summary>着色器阶段对象，必须同时包含顶点与片元。</summary>
    public IShader[] Shaders { get; set; }
}
