using System.Collections;

namespace Line.Framework.Types;

/// <summary>
/// 长数组
/// </summary>
/// <typeparam name="数据类型"></typeparam>
public class LongArray<T> : IEnumerable<T>
{
    private const int PageSize = 1 << 20; // 每页 1,048,576 个元素（1M）
    private readonly List<T[]> _pages;

    public IEnumerator<T> GetEnumerator()
    {
        for (long i = 0; i < Length; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 数组长度
    /// </summary>
    public long Length { get; }

    public LongArray(long length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        long pageCount = (length + PageSize - 1) / PageSize;
        _pages = new List<T[]>((int)pageCount);
        for (long i = 0; i < pageCount; i++)
        {
            int size = (int)Math.Min(PageSize, length - i * PageSize);
            _pages.Add(new T[size]);
        }
    }

    public T this[long index]
    {
        get
        {
            if (index < 0 || index >= Length)
                throw new IndexOutOfRangeException();
            int pageIndex = (int)(index / PageSize);
            int offset = (int)(index % PageSize);
            return _pages[pageIndex][offset];
        }
        set
        {
            if (index < 0 || index >= Length)
                throw new IndexOutOfRangeException();
            int pageIndex = (int)(index / PageSize);
            int offset = (int)(index % PageSize);
            _pages[pageIndex][offset] = value;
        }
    }
}
