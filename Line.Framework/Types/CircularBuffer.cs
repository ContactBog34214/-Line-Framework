using System.Collections;
using System.Collections.Generic;

namespace Line.Framework.Types;

/// <summary>
/// 环形缓冲区
/// </summary>
/// <typeparam name="类型"></typeparam>
public class CircularBuffer<T> : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        for (long i = 0; i < Count; i++)
        {
            yield return Buffer[i];
        }
    }

    private readonly Lock _lock = new();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private LongArray<T> Buffer;

    /// <summary>
    /// 缓冲区大小
    /// </summary>
    public long Size
    {
        get;
        set
        {
            if (value < 0)
                throw new InvalidDataException($"Buffer Size cannot be {value}");
            field = value;
            Reset();
        }
    }
    private long AddPtr = 0;
    private bool Circulated = false;

    /// <summary>
    /// 缓冲区数据数量
    /// </summary>
    public long Count
    {
        get => Circulated ? Size : AddPtr;
    }
    public T this[long i]
    {
        get
        {
            void throwOutsideTheBounds() =>
                throw new ArgumentOutOfRangeException(
                    nameof(i),
                    i,
                    "Index was outside the bounds of the array."
                );
            if (i < 0)
                throwOutsideTheBounds();
            if (Circulated)
            {
                if (i >= Size)
                    throwOutsideTheBounds();
                var tg = (AddPtr + i) % Size;
                return Buffer[tg];
            }
            if (i >= Count)
                throwOutsideTheBounds();
            return Buffer[i];
        }
    }

    /// <summary>
    /// 写入缓冲区
    /// </summary>
    /// <param name="写入数据"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Add(T value)
    {
        if (Size <= 0)
            throw new InvalidOperationException("Cannot add to a buffer of size 0.");
        lock (_lock)
        {
            Buffer[AddPtr] = value;
            AddPtr++;
            if (AddPtr >= Size)
            {
                AddPtr = 0;
                Circulated = true;
            }
        }
    }

    /// <summary>
    /// 重置缓冲区
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Buffer = new(Size);
            AddPtr = 0;
            Circulated = false;
        }
    }

    /// <summary>逻辑下标（0 = 最旧）→ 物理下标。</summary>
    private long PhysicalIndex(long logical)
    {
        if (!Circulated)
            return logical;

        return (AddPtr + logical) % Size;
    }

    /// <summary>
    /// 取走最旧的一个元素。容量不变。
    /// </summary>
    public bool TryDequeue(out T value)
    {
        lock (_lock)
        {
            if (Count == 0)
            {
                value = default;
                return false;
            }

            value = Buffer[PhysicalIndex(0)];
            RemoveAtUnlocked(0);
            return true;
        }
    }

    /// <summary>
    /// 删除逻辑顺序上的第 <paramref name="index"/> 个元素（0 = 最旧）。
    /// <b>容量不变</b>，只是把后面元素前移。
    /// </summary>
    public bool RemoveAt(long index)
    {
        lock (_lock)
            return RemoveAtUnlocked(index);
    }

    /// <summary>
    /// 删除第一个等于 <paramref name="value"/> 的元素。
    /// </summary>
    public bool Remove(T value)
    {
        lock (_lock)
        {
            var cmp = EqualityComparer<T>.Default;
            long count = Count;
            for (long i = 0; i < count; i++)
            {
                if (cmp.Equals(Buffer[PhysicalIndex(i)], value))
                    return RemoveAtUnlocked(i);
            }

            return false;
        }
    }

    /// <summary>
    /// 删除实现。<b>调用方必须已持有 <see cref="_lock"/></b>。
    /// <para>
    /// 不能就地压缩：环绕状态下逻辑下标 i 的物理位置可能正好是刚被覆盖过的位置，
    /// 直接 <c>Buffer[i] = this[i + 1]</c> 会读到脏数据。所以先整体搬到临时数组。
    /// </para>
    /// </summary>
    private bool RemoveAtUnlocked(long index)
    {
        long count = Count;
        if (index < 0 || index >= count)
            return false;

        if (scratch is null || scratch.Length < Size)
            scratch = new T[Size];

        long w = 0;
        for (long i = 0; i < count; i++)
        {
            if (i == index)
                continue;

            scratch[w++] = Buffer[PhysicalIndex(i)];
        }

        for (long i = 0; i < w; i++)
            Buffer[i] = scratch[i];

        // 尾部清空，避免残留引用（对 struct 无害，但对引用类型是必需的）。
        for (long i = w; i < Size; i++)
            Buffer[i] = default;

        AddPtr = w;
        Circulated = false;
        return true;
    }

    /// <summary>供 RemoveAt 中转用的临时区，避免每次分配。</summary>
    private T[] scratch;

    public CircularBuffer(long Size)
    {
        this.Size = Size;
    }
}
