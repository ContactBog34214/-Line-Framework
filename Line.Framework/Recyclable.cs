using System.Collections.Concurrent;

namespace Line.Framework;

public interface IRecyclable
{
    void Reset();
}

public static class Recyclable
{
    private static readonly ConcurrentDictionary<Type, ConcurrentBag<IRecyclable>> ObjectPool = [];
    private static bool FindPool<T>(bool createWhenMiss, out ConcurrentBag<IRecyclable> result) where T : class, IRecyclable
    {
        Type type = typeof(T);
        ConcurrentBag<IRecyclable> pool = null;
        if (pool == null) ObjectPool.TryGetValue(type, out pool);
        bool exist = pool != null;
        if (createWhenMiss) pool ??= [];
        result = pool;
        return exist;
    }
    public static T New<T>() where T : class, IRecyclable, new()
    {
        Type type = typeof(T);
        bool exist = FindPool<T>(true, out var pool);
        pool ??= [];
        IRecyclable request = default;
        if (!pool.TryTake(out request)) request = new T();
        T result = (T)request;
        result.Reset();
        if (!exist) ObjectPool.TryAdd(type, pool);
        return result;
    }
    public static T Get<T>() where T : class, IRecyclable
    {
        Type type = typeof(T);
        bool exist = FindPool<T>(true, out var pool);
        pool ??= [];
        IRecyclable request = default;
        if (!pool.TryTake(out request)) return default;
        T result = (T)request;
        result.Reset();
        if (!exist) ObjectPool.TryAdd(type, pool);
        return result;
    }
    public static void Free<T>(this T obj) where T : class, IRecyclable
    {
        Type type = typeof(T);
        bool exist = FindPool<T>(true, out var pool);
        pool ??= [];
        if (!exist) ObjectPool.TryAdd(type, pool);
        pool.Add(obj);
    }
    public static int Count<T>() where T : class, IRecyclable
    {
        Type type = typeof(T);
        bool exist = FindPool<T>(false, out var pool);
        return !exist ? 0 : pool.Count;
    }
    public static bool Clear<T>() where T : class, IRecyclable
    {
        Type type = typeof(T);
        return ObjectPool.TryRemove(type, out _);
    }
    public static void Limit<T>(int maximumObjectCount) where T : class, IRecyclable
    {
        if (!FindPool<T>(true, out var pool)) return;
        int dn = Math.Max(0, pool.Count - maximumObjectCount);
        if (dn <= 0) return;
        Parallel.For(0, dn, i => pool.TryTake(out _));
    }
}

public static class Recyclable<T> where  T : class, IRecyclable, new()
{
    public static bool Enabled { get; set; }= true;
    private static readonly ConcurrentBag<T> pool = [];
    /// <summary>
    /// It is a soft limiter
    /// </summary>
    public static int? Limit { get; set; } = null;
    public static T New()
    {
        if(!Enabled)return new();
        if (!pool.TryTake(out var request)) request = new();
        T result = request;
        result.Reset();
        return result;
    }
    public static T Get()
    {
        if(!Enabled)return default;
        if (!pool.TryTake(out var request)) return default;
        T result = request;
        result.Reset();
        return result;
    }
    public static void Free(T obj)
    {
        if(!Enabled)return;
        if (Limit != null)
        {
            if(Limit<=pool.Count)return;
        }
        pool.Add(obj);
    }
    public static int Count()
    {
        return pool.Count;
    }
    public static void Release()
    {
        pool.Clear();
    }
    public static void ApplyLimit(int? limit = null)
    {
        limit ??= Limit;

        if (limit is not int max)
            return;

        int removeCount = Math.Max(0, pool.Count - max);

        for (int i = 0; i < removeCount; i++)
            pool.TryTake(out _);
    }
}