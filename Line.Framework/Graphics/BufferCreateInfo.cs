namespace Line.Framework.Graphics;

/// <summary>
/// 创建缓冲（<see cref="IDeviceBuffer"/>）的参数。
/// <para>
/// 注意各项的组合约束：
/// <list type="bullet">
/// <item>作为 uniform buffer 使用时，<see cref="Usage"/> 必须包含
///       <see cref="BufferUsage.Uniform"/>，否则后端在绑定资源集时会拒绝。</item>
/// <item><see cref="StructureByteStride"/> 只在 storage buffer 时需要，
///       用于描述结构体大小；普通顶点 / uniform 缓冲留 0。</item>
/// </list>
/// </para>
/// </summary>
public struct BufferCreateInfo
{
    /// <summary>缓冲大小（字节）。必须显式赋值，0 会被后端拒绝。</summary>
    public ulong SizeInBytes { get; set; }

    /// <summary>用途标志，决定这块缓冲能被怎样使用。</summary>
    public BufferUsage Usage { get; set; }

    /// <summary>
    /// 结构体步长（字节），仅 storage buffer 有意义。
    /// 0 表示不是结构化缓冲。
    /// </summary>
    public uint StructureByteStride { get; set; }
}
