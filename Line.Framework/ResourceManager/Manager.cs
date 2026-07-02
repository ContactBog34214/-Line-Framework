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

    Dictionary<string, IResource> Resources = [];
    Dictionary<string, (ulong LastGetTime, ulong NumGet)> Rs = [];
    Dictionary<string, ResourceType> Types = [];
    Stopwatch sw = new();
    private readonly Timer _releaseTimer;
    private readonly int _releaseIntervalMs;

    public ResourceManager()
    {
        sw.Start();
        _releaseIntervalMs = 1500;
        _releaseTimer = new Timer(OnReleaseTimer, null, _releaseIntervalMs, _releaseIntervalMs);
    }

    void OnReleaseTimer(object? a)
    {
        ReleaseIdleResources();
    }

    public void AddResource(string id, IResource res)
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

    public object GetResource(string id)
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

    public List<string> GetAllResourceId()
    {
        List<string> a = [];
        a.AddRange(Resources.Keys);
        return a;
    }

    public void ReleaseIdleResources()
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
                Log.Error($" [ResourceManager]Release faild:{ex}");
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

    public void AddType(string id, ResourceType t)
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

    public ResourceType GetResourceType(string id)
    {
        if (Types.TryGetValue(id, out var obj))
        {
            return obj;
        }
        return null;
    }

    public List<string> GetAllTypeId()
    {
        List<string> a = [];
        a.AddRange(Types.Keys);
        return a;
    }

    public void Create(string TypeId, string targetId, Stream stream)
    {
        if (!Types.TryGetValue(TypeId, out var t))
        {
            return;
        }
        if (Resources.TryGetValue(targetId, out _))
        {
            return;
        }
        try{
        t.Create(targetId, stream);}catch(Exception ex)
        {
            Log.Error($"[ResourceManager] {ex}");
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
