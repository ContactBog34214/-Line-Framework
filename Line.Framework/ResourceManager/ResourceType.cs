namespace Line.Framework.Resource;

public abstract class ResourceType
{
    public abstract void Create(string id, Stream stream);
    internal ResourceManager Manager { get; init; }

    public ResourceType(ResourceManager manager)
    {
        Manager = manager;
    }
}
