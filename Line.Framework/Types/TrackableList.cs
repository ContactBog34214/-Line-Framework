using System.Collections;
using System.Collections.Generic;
using TagLib.Matroska;

namespace Line.Framework.Types;

public class TrackableList<T> : IEnumerable<T> // 1. 继承 IEnumerable<T>
{
    private readonly List<T> _list = new();
    public bool IsDirty { get; private set; }

    // 添加
    public void Add(T item)
    {
        _list.Add(item);
        IsDirty = true;
    }

    // 批量添加（可选）
    public void AddRange(IEnumerable<T> items)
    {
        _list.AddRange(items);
        IsDirty = true;
    }

    // 删除
    public bool Remove(T item)
    {
        if (_list.Remove(item))
        {
            IsDirty = true;
            return true;
        }
        return false;
    }

    // 清空
    public void Clear()
    {
        _list.Clear();
        IsDirty = true;
    }

    // 索引器（修改元素会触发脏标志）
    public T this[int index]
    {
        get => _list[index];
        set
        {
            _list[index] = value;
            IsDirty = true;
        }
    }

    // 获取内部只读副本（用于安全地读取）
    public IReadOnlyList<T> AsReadOnly() => _list.AsReadOnly();

    // 重置脏标志（保存数据后调用）
    public void ResetDirty() => IsDirty = false;

    // 2. 关键修复：实现 GetEnumerator，支持 foreach
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    // 3. 显式实现非泛型接口（C# 语法要求）
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public int Count => _list.Count;

    public static implicit operator List<T>(TrackableList<T> values)
    {
        return [.. values._list];
    }

    public static implicit operator TrackableList<T>(List<T> values)
    {
        TrackableList<T> r = new(values);
        r.IsDirty = false;
        return r;
    }

    public TrackableList(List<T> values = null)
    {
        _list = values ?? new();
        IsDirty = false;
    }
}
