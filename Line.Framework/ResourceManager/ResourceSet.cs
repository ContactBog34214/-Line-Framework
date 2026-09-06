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
        return new ResourceSetArg(t, resourceSet);
    }

    public async Task Load()
    {
        if (ImgStream == null) return;
        if (resourceSet != null)
            return;
        ImgStream.Position = 0;
        var image = new ImageSharpTexture(ImgStream, EnableMipmap);
        if (t != null)
            try
            {
                t?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning($"Cannot Dispose Texture:{ex}");
            }
        t = image.CreateDeviceTexture(parent.Dev, parent.Dev.ResourceFactory);
        if (t == null)
            return;
        resourceSet = parent.Dev.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(parent.Layout, t)
        );
    }

    public async Task Release()
    {
        resourceSet?.Dispose();
        resourceSet = null;
        t?.Dispose();
        t = null;
    }

    public void Dispose()
    {
        ImgStream?.Dispose();
        Release().GetAwaiter().GetResult();
        if (!(t?.IsDisposed ?? true)) t.Dispose();
    }

    public RResourceSet(Stream stream, TResourceSet tr)
    {
        parent = tr;
        if (stream.CanSeek) stream.Position = 0;
        ImgStream = new MemoryStream();
        stream.CopyTo(ImgStream);
    }
    private readonly Stream ImgStream;
    public bool EnableMipmap
    {
        get; set
        {
            if (value == field) return;
            field = value;
            var tmp = IsLoaded;
            Release().GetAwaiter().GetResult();
            if (tmp) Load().GetAwaiter().GetResult();
        }
    } = true;
}

public class TResourceSet : ResourceType
{
    internal GraphicsDevice Dev { get; init; }
    internal ResourceLayout Layout { get; init; }

    public TResourceSet(GraphicsDevice dev, ResourceLayout layout)
    {
        Dev = dev;
        Layout = layout;
    }

    public override async Task<IResource> Create(Stream stream)
    {
        return new RResourceSet(stream, this);
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
