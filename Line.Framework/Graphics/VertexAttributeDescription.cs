namespace Line.Framework.Graphics;

/// <summary>
/// 单个顶点属性的描述。
/// <para>
/// <see cref="Offset"/> 是该属性在顶点结构体内的字节偏移，
/// 必须与 C# 侧实际上传的内存布局完全一致 —— 错位不会报错，
/// 只会让着色器读到垃圾数据。
/// </para>
/// </summary>
public struct VertexAttributeDescription
{
    /// <summary>属性格式（分量类型与个数，是否归一化）。</summary>
    public VertexFormat Format { get; set; }

    /// <summary>相对顶点起始地址的字节偏移。</summary>
    public uint Offset { get; set; }

    /// <summary>
    /// 属性的字节大小。多数后端由 <see cref="Format"/> 推导，
    /// 留 0 即可；显式填写也不会出错。
    /// </summary>
    public uint SizeInBytes { get; set; }
}
