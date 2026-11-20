namespace Line.Framework.Graphics;

/// <summary>
/// 索引缓冲的元素宽度。
/// <para>
/// 只影响 GPU 如何解释索引缓冲里的字节，不影响 <see cref="BufferUsage.Index"/>
/// 的声明 —— 创建索引缓冲时两者都要给：前者在
/// <see cref="ICommandList.SetIndexBuffer"/> 里传，后者在
/// <see cref="BufferCreateInfo"/> 里传。
/// </para>
/// <para>
/// <b>选哪种</b>：顶点数 &lt; 65536 用 <see cref="UInt16"/>，显存和带宽都省一半；
/// 更大的网格必须用 <see cref="UInt32"/>。上界判断错了不会报错，
/// 只会读到回绕后的错误顶点（表现为网格乱成一片）。
/// </para>
/// </summary>
public enum IndexFormat
{
    /// <summary>2 字节无符号整数。</summary>
    UInt16,

    /// <summary>4 字节无符号整数。</summary>
    UInt32,
}
