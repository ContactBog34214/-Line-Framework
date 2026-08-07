namespace Line.Framework.Resource;

public abstract class ResourceType
{
    public abstract Task<IResource> Create(Stream stream);
}
