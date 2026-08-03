using System.Collections;

namespace Line.Framework.Types;

public class IndexList<T> : IEnumerable<T>
    where T : class, IIndexable
{
    private readonly List<T> _items = new();

    // 索引器（支持负数）
    public T this[int i]
    {
        get
        {
            int index = NormalizeIndex(i);
            return _items[index];
        }
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            int index = NormalizeIndex(i);
            T oldItem = _items[index];

            if (oldItem.Index == value.Index)
            {
                _items[index] = value;
            }
            else
            {
                _items.RemoveAt(index);
                int insertIndex = _items.BinarySearch(value, _comparer);
                if (insertIndex >= 0)
                    _items[insertIndex] = value;
                else
                    _items.Insert(~insertIndex, value);
            }
        }
    }

    public int Add(T value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        int index = _items.BinarySearch(value, _comparer);
        if (index >= 0)
            return index; // 已存在相同 Index 的元素

        int insertIndex = ~index;
        _items.Insert(insertIndex, value);
        return insertIndex;
    }

    public int Count => _items.Count;

    // 辅助：规范化负数索引并做边界检查
    private int NormalizeIndex(int i)
    {
        if (i >= 0 && i < _items.Count)
            return i;
        if (i < 0)
        {
            int idx = _items.Count + i;
            if (idx >= 0 && idx < _items.Count)
                return idx;
        }
        throw new IndexOutOfRangeException(
            $"Index {i} is out of range. Valid range: [{-_items.Count}, {_items.Count - 1}]"
        );
    }

    private static readonly ItemComparer _comparer = new();

    private sealed class ItemComparer : IComparer<T>
    {
        public int Compare(T x, T y)
        {
            // 由于 T 有 IIndexable 约束且我们确保插入非空，这里可安全比较
            return x.Index.CompareTo(y.Index);
        }
    }

    public void Remove(int i)
    {
        if (i < 0)
            i = _items.Count + i;
        _items.RemoveAt(i);
    }

    public void Remove(T value)
    {
        _items.Remove(value);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public bool Contains(T value) => _items.BinarySearch(value, _comparer) >= 0;

    public int IndexOf(T value) => _items.BinarySearch(value, _comparer);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
