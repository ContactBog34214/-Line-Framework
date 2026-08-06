using Veldrid;
using Veldrid.ImageSharp;

namespace Line.Framework.Resource.Graphic;

public class RResourceSet : IResource
{
    public bool IsLoaded
    {
        get
        {
            if (resourceSet != null)
                return !resourceSet.IsDisposed;
            return false;
        }
    }
    ResourceSet resourceSet;
    TResourceSet parent;
    Texture t;

    public object GetHandle()
    {
        return new ResourceSetArg(t,resourceSet);
    }

    public async Task Load()
    {
        if (t == null)
            return;
        if (resourceSet != null)
            return;
        resourceSet = parent.Dev.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(parent.Layout, t)
        );
    }

    public async Task Release()
    {
        if (resourceSet == null)
            return;
        resourceSet.Dispose();
        resourceSet = null;
    }

    public void Dispose()
    {
        Release().GetAwaiter().GetResult();
        t.Dispose();
    }

    public RResourceSet(Stream stream, TResourceSet tr)
    {
        parent = tr;
        var image = new ImageSharpTexture(stream);
        t = image.CreateDeviceTexture(parent.Dev, parent.Dev.ResourceFactory);
    }
}

public class TResourceSet : ResourceType
{
    internal GraphicsDevice Dev { get; init; }
    internal ResourceLayout Layout { get; init; }

    public TResourceSet(ResourceManager manager, GraphicsDevice dev, ResourceLayout layout)
        : base(manager)
    {
        Dev = dev;
        Layout = layout;
    }

    public override async Task Create(string id, Stream stream)
    {
        var tmp = new RResourceSet(stream, this);
        Manager.AddResource(id, tmp);
    }
}

public class ResourceSetArg
{
    public Texture Texture { get; init; }
    public ResourceSet ResourceSet { get; init; }

    internal ResourceSetArg(Texture t, ResourceSet rs)
    {
        Texture = t;
        ResourceSet = rs;
    }
}
