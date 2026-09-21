using System.Collections.Concurrent;

namespace Line.Framework;

public interface IRecycable
{
    void Reset();
}

public static class Recycable
{
    private static readonly ConcurrentDictionary<string, ConcurrentBag<IRecycable>> ObjectPool = [];
    public static T New<T>() where T : IRecycable, new()
    {
        string fn = typeof(T).FullName;
        bool exist = ObjectPool.TryGetValue(fn, out var pool);
        pool ??= [];
        IRecycable request = default;
        if (!pool.TryTake(out request)) request = new T();
        T result = (T)request;
        if (Equals(result, default)) result = new();
        result.Reset();
        if (!exist) ObjectPool.TryAdd(fn, pool);
        return result;
    }
    public static void Free<T>(this T obj) where T : IRecycable
    {
        string fn = typeof(T).FullName;
        bool exist = ObjectPool.TryGetValue(fn, out var pool);
        pool ??= [];
        if (!exist) ObjectPool.TryAdd(fn, pool);
        pool.Add(obj);
    }
}