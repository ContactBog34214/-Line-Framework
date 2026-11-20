namespace Line.Framework.Graphics
{
    /// <summary>
    /// 顶点属性格式：分量的类型、个数，以及是否归一化。
    /// <para>
    /// 命名规则：<c>类型 + 分量数 [Norm]</c>。
    /// <c>Norm</c> 表示把整数分量映射到 [0,1]（无符号）或 [-1,1]（有符号）再交给着色器，
    /// 因此在 GLSL 里仍然声明为 <c>float</c> 系列。
    /// </para>
    /// <para>
    /// <b>必须与 C# 侧结构体字段严格对应</b>：声明成 <c>Float4</c> 就要真的
    /// 传 4 个 float；声明成 <c>UByte4Norm</c> 就要传 4 个字节。
    /// 不匹配不会报错，只会读到错位数据。
    /// </para>
    /// </summary>
    public enum VertexFormat
    {
        /// <summary>1 个 float。</summary>
        Float1,

        /// <summary>2 个 float（二维位置、UV 常用）。</summary>
        Float2,

        /// <summary>3 个 float（三维位置、法线常用）。</summary>
        Float3,

        /// <summary>4 个 float（颜色、切线常用）。</summary>
        Float4,

        /// <summary>1 个 int32。</summary>
        Int1,

        /// <summary>2 个 int32。</summary>
        Int2,

        /// <summary>3 个 int32。</summary>
        Int3,

        /// <summary>4 个 int32。</summary>
        Int4,

        /// <summary>1 个 uint32。</summary>
        UInt1,

        /// <summary>2 个 uint32。</summary>
        UInt2,

        /// <summary>3 个 uint32。</summary>
        UInt3,

        /// <summary>4 个 uint32。</summary>
        UInt4,

        /// <summary>2 个 int16（不归一化）。</summary>
        Short2,

        /// <summary>4 个 int16（不归一化）。</summary>
        Short4,

        /// <summary>2 个 int16，归一化到 [-1,1]。</summary>
        Short2Norm,

        /// <summary>4 个 int16，归一化到 [-1,1]。</summary>
        Short4Norm,

        /// <summary>2 个 uint16（不归一化）。</summary>
        UShort2,

        /// <summary>4 个 uint16（不归一化）。</summary>
        UShort4,

        /// <summary>2 个 uint16，归一化到 [0,1]。</summary>
        UShort2Norm,

        /// <summary>4 个 uint16，归一化到 [0,1]。</summary>
        UShort4Norm,

        /// <summary>2 个 half（16 位浮点）。</summary>
        Half2,

        /// <summary>4 个 half（16 位浮点）。</summary>
        Half4,

        /// <summary>2 个 int8（不归一化）。</summary>
        Byte2,

        /// <summary>4 个 int8（不归一化）。</summary>
        Byte4,

        /// <summary>2 个 int8，归一化到 [-1,1]。</summary>
        Byte2Norm,

        /// <summary>4 个 int8，归一化到 [-1,1]。</summary>
        Byte4Norm,

        /// <summary>2 个 uint8（不归一化）。</summary>
        UByte2,

        /// <summary>4 个 uint8（不归一化）。UI 顶点色可用它把颜色压到 4 字节。</summary>
        UByte4,

        /// <summary>2 个 uint8，归一化到 [0,1]。</summary>
        UByte2Norm,

        /// <summary>4 个 uint8，归一化到 [0,1]（紧凑的顶点色表示）。</summary>
        UByte4Norm,
    }
}
