namespace Line.Framework.Resource;

public interface IResource : IDisposable
{
    bool IsLoaded { get; }
    object GetHandle();
    Task Load();
    Task Release();
}