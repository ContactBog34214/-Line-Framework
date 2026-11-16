using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 设备缓冲：GPU 可读的线性内存块。
/// <para>
/// 用途由创建时的 <see cref="BufferUsage"/> 决定，同一个对象只能是
/// 顶点缓冲、索引缓冲、uniform buffer 等角色中的某几种组合。
/// </para>
/// <para>
/// <b>上传模型的差异</b>：Veldrid 直接写入（可能走 staging 中转）；
/// SDL3 每次 <see cref="Update"/> 都会额外提交一个拷贝命令缓冲，
/// 因此应当「攒好一帧的数据再调一次 Update」，而不是逐个字段更新。
/// </para>
/// </summary>
public interface IDeviceBuffer : IDisposable, IName
{
    /// <summary>缓冲总大小（字节）。</summary>
    ulong SizeInBytes { get; }

    /// <summary>创建时声明的用途。</summary>
    BufferUsage Usage { get; }

    /// <summary>
    /// 把数据写入缓冲的指定位置。
    /// </summary>
    /// <param name="offset">目标缓冲内的字节偏移。</param>
    /// <param name="source">源数据；长度 + offset 不得超出缓冲大小。</param>
    void Update(ulong offset, ReadOnlySpan<byte> source);
}
