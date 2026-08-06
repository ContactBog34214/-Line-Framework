using System.Diagnostics;

namespace Line.Framework.Resource;

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

    readonly Dictionary<string, IResource> Resources = [];
    public bool AutoReleaseResources { get; set; } = true;
    readonly Dictionary<string, (ulong LastGetTime, ulong NumGet)> Rs = [];
    readonly Dictionary<string, ResourceType> Types = [];
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
            Resources.Remove(id);
        }
        Resources.TryAdd(id, res);
        Rs.TryAdd(id, new((uint)sw.ElapsedMilliseconds, 0));
    }

    public virtual async Task<object> GetResource(string id)
    {
        if (id == null)
            return null;
        if (!Resources.TryGetValue(id, out var obj))
        {
            return null;
        }
        var target = obj;
        if (!target.IsLoaded)
            target.Load();
        try
        {
            var t = Rs[id];
            t.LastGetTime = (ulong)sw.ElapsedMilliseconds;
            t.NumGet++;
            Rs[id] = t;
        }
        catch { }
        return target.GetHandle();
    }

    public virtual List<string> GetAllResourceId()
    {
        List<string> a = [];
        a.AddRange(Resources.Keys);
        return a;
    }

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
                    Resources[i].Release();
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

    public void DisposeResource(string id)
    {
        if (!Resources.TryGetValue(id, out var obj))
        {
            return;
        }
        obj.Release();
        obj.Dispose();
        Resources.Remove(id);
    }

    public virtual void AddType(string id, ResourceType t)
    {
        if (t == null)
        {
            return;
        }
        if (Types.TryGetValue(id, out _))
        {
            Types.Remove(id);
        }
        Types.TryAdd(id, t);
    }

    public virtual ResourceType GetResourceType(string id)
    {
        if (Types.TryGetValue(id, out var obj))
        {
            return obj;
        }
        return null;
    }

    public virtual List<string> GetAllTypeId()
    {
        List<string> a = [];
        a.AddRange(Types.Keys);
        return a;
    }

    public virtual async Task Create(string TypeId, string targetId, Stream stream)
    {
        if (!Types.TryGetValue(TypeId, out var t))
        {
            return;
        }
        if (Resources.TryGetValue(targetId, out _))
        {
            return;
        }
        try
        {
            await t.Create(targetId, stream);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex}");
        }
    }

    public void DisposeType(string id)
    {
        if (!Types.TryGetValue(id, out _))
        {
            return;
        }
        Types.Remove(id);
    }
}
