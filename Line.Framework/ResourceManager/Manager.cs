using System.Collections.Concurrent;
using System.Diagnostics;

namespace Line.Framework.Resource;

/// <summary>
/// 资产管理器
/// </summary>
public class ResourceManager : IDisposable
{
    public void Dispose()
    {
        List<IResource> resources = [];
        resources.AddRange(Resources.Values);
        foreach (var i in resources)
        {
            i.Release();
            i.Dispose();
        }
        Types.Clear();
    }

    readonly ConcurrentDictionary<string, IResource> Resources = [];
    public bool AutoReleaseResources { get; set; } = true;
    readonly Dictionary<string, (ulong LastGetTime, ulong NumGet)> Rs = [];
    readonly ConcurrentDictionary<string, ResourceType> Types = [];
    readonly Stopwatch sw = new();

    public ResourceManager()
    {
        sw.Start();
        _ = new Timer(async (a) => await OnReleaseTimer(a), null, 1500, 1500);
    }

    async Task OnReleaseTimer(object a)
    {
        if (AutoReleaseResources)
            await ReleaseIdleResources();
    }

    /// <summary>
    /// 向资产管理器添加新(外来)资产
    /// </summary>
    /// <param name="资产ID"></param>
    /// <param name="资产对象"></param>
    public virtual void AddResource(string id, IResource res)
    {
        if (res == null)
        {
            return;
        }
        if (Resources.TryGetValue(id, out var obj))
        {
            obj.Release();
            obj.Dispose();
            Resources.TryRemove(id, out _);
        }
        Resources.TryAdd(id, res);
        Rs.TryAdd(id, new((uint)sw.ElapsedMilliseconds, 0));
    }

    /// <summary>
    /// 获取指定ID的资产
    /// </summary>
    /// <typeparam name="资产实际类型"></typeparam>
    /// <param name="资产ID"></param>
    /// <param name="是否在未加载时自动加载"></param>
    /// <returns>资产</returns>
    public virtual async Task<T> GetResource<T>(string id, bool NeedLoaded = true)
    {
        if (id == null)
            return default;
        if (!Resources.TryGetValue(id, out var obj))
        {
            return default;
        }
        var target = obj;
        if (NeedLoaded)
            await LoadResource(id);
        try
        {
            var t = Rs[id];
            t.LastGetTime = (ulong)sw.ElapsedMilliseconds;
            t.NumGet++;
            Rs[id] = t;
        }
        catch { }
        if (target.IsLoaded)
            return (T)target.GetHandle();
        return default;
    }

    /// <summary>
    /// 加载指定ID的资产
    /// </summary>
    /// <param name="资产ID"></param>
    /// <returns></returns>
    public virtual async Task LoadResource(string id)
    {
        if (id == null)
            return;
        if (!Resources.TryGetValue(id, out var obj))
        {
            return;
        }
        if (!obj.IsLoaded)
            await obj.Load();
    }

    /// <summary>
    /// 获取资产是否加载
    /// </summary>
    /// <param name="资产ID"></param>
    /// <returns>加载状态</returns>
    public virtual bool ResourceIsLoaded(string id)
    {
        if (id == null)
            return false;
        if (!Resources.TryGetValue(id, out var obj))
        {
            return false;
        }
        return obj.IsLoaded;
    }

    /// <summary>
    /// 释放指定资产ID的资源
    /// </summary>
    /// <param name="资产ID"></param>
    /// <returns></returns>
    public virtual async Task ReleaseResource(string id)
    {
        if (id == null)
            return;
        if (!Resources.TryGetValue(id, out var obj))
        {
            return;
        }
        await obj.Release();
    }

    /// <summary>
    /// 获取所有资产的ID
    /// </summary>
    /// <returns>资产ID(List)</returns>
    public virtual List<string> GetAllResourceId()
    {
        List<string> a = [];
        a.AddRange(Resources.Keys);
        return a;
    }

    /// <summary>
    /// 释放所有不活跃的资产
    /// </summary>
    /// <returns></returns>
    public virtual async Task ReleaseIdleResources()
    {
        foreach (var i in Resources.Keys)
        {
            try
            {
                var rsi = Rs[i];
                ulong Time = (ulong)sw.ElapsedMilliseconds - rsi.LastGetTime;
                if (Time > 120000 || Time > rsi.NumGet * 300)
                {
                    rsi.LastGetTime = (ulong)sw.ElapsedMilliseconds;
                    await Resources[i].Release();
                    if (Resources[i].IsLoaded)
                        rsi.LastGetTime += 1000 * 30;
                }
                else
                {
                    rsi.NumGet = (ulong)(rsi.NumGet / 1.1);
                }
                Rs[i] = rsi;
            }
            catch (Exception ex)
            {
                Log.Error($"Release faild:{ex}");
            }
        }
    }

    /// <summary>
    /// 注销指定资产
    /// </summary>
    /// <param name="资产ID"></param>
    public void DisposeResource(string id)
    {
        if (!Resources.TryGetValue(id, out var obj))
        {
            return;
        }
        obj.Release();
        obj.Dispose();
        Resources.TryRemove(id, out _);
    }

    /// <summary>
    /// 注销指定资产
    /// </summary>
    /// <param name="资产对象"></param>
    public void DisposeResource(IResource resource)
    {
        if (Resources.Values.ToList().IndexOf(resource) == -1)
        {
            return;
        }
        foreach (var i in Resources)
        {
            if (i.Value != resource)
                continue;
            i.Value.Release();
            i.Value.Dispose();
            Resources.TryRemove(i.Key, out _);
            return;
        }
    }

    /// <summary>
    /// 添加资产构建器
    /// </summary>
    /// <param name="构建器ID"></param>
    /// <param name="构建器对象"></param>
    public virtual void AddType(string id, ResourceType t)
    {
        if (t == null)
        {
            return;
        }
        if (Types.TryGetValue(id, out _))
        {
            Types.TryRemove(id, out _);
        }
        Types.TryAdd(id, t);
    }

    /// <summary>
    /// 获取资产构建器
    /// </summary>
    /// <param name="构建器ID"></param>
    /// <returns>构建器对象</returns>
    public virtual ResourceType GetResourceType(string id)
    {
        if (Types.TryGetValue(id, out var obj))
        {
            return obj;
        }
        return null;
    }

    /// <summary>
    /// 获取所有资产构建器
    /// </summary>
    /// <returns>所有资产构建器(List)</returns>
    public virtual List<string> GetAllTypeId()
    {
        List<string> a = [];
        a.AddRange(Types.Keys);
        return a;
    }

    /// <summary>
    /// 使用构建器创建资产
    /// </summary>
    /// <param name="构建器ID"></param>
    /// <param name="资产ID"></param>
    /// <param name="数据流"></param>
    /// <returns>创建后的资产对象</returns>
    public virtual async Task<IResource> Create(string TypeId, string targetId, Stream stream)
    {
        if (!Types.TryGetValue(TypeId, out var t))
        {
            return null;
        }
        if (Resources.TryGetValue(targetId, out _))
        {
            return null;
        }
        try
        {
            var res = await t.Create(stream);
            AddResource(targetId, res);
            return res;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }
        return null;
    }

    /// <summary>
    /// 注销指定资产构建器
    /// </summary>
    /// <param name="资产构建器ID"></param>
    public void DisposeType(string id)
    {
        if (!Types.TryGetValue(id, out _))
        {
            return;
        }
        Types.TryRemove(id, out _);
    }
}
