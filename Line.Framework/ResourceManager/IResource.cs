namespace Line.Framework.Resource;

public interface IResource : IDisposable
{
    bool IsLoaded { get; }
    object GetHandle();
    void Load();
    void Release();
}