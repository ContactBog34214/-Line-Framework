namespace Line.Framework.Graphics;

/// <summary>
/// 一个顶点缓冲的布局：步长 + 属性列表。
/// <para>
/// <b>属性顺序即着色器的 location 顺序</b>（location = 数组下标），
/// 所以第一个属性对应 <c>layout(location = 0)</c>。
/// </para>
/// <para>
/// <b>步长必须与 C# 侧结构体大小一致</b>：若结构体是
/// <c>{ vec2 Position; vec4 Color; vec2 UV; }</c>，步长应为 8 + 16 + 8 = 32。
/// 填错同样不报错，只会让每个顶点读错位置。
/// </para>
/// </summary>
public struct VertexLayoutDescription
{
    /// <summary>属性列表，顺序即 location。</summary>
    public VertexAttributeDescription[] Attributes { get; set; }

    /// <summary>每个顶点（或实例）占用的字节数。</summary>
    public uint Stride { get; set; }

    /// <summary>该缓冲按顶点推进还是按实例推进。</summary>
    public VertexInputRate InputRate { get; set; }
}
