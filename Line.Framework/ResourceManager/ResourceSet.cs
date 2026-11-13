using Line.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Line.Framework.Resource.Graphic;

public class RResourceSet : IResource
{
    public bool IsLoaded
    {
        get { return resourceSet != null; }
    }
    IResourceSet resourceSet;
    TResourceSet parent;
    ITexture t;

    public object GetHandle()
    {
        return new ResourceSetArg(t, resourceSet);
    }

    public async Task Load()
    {
        if (ImgStream == null)
            return;
        if (resourceSet != null)
            return;
        if (t != null)
            try
            {
                t?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning($"Cannot Dispose Texture:{ex}");
            }

        //CreateTexture
        Image img = null;
        try
        {
            ImgStream.Position = 0;
            img = await Image.LoadAsync<Rgba32>(ImgStream);
            t = parent.Dev.ResourceFactory.CreateTexture(
                new()
                {
                    Width = (uint)img.Width,
                    Height = (uint)img.Height,
                    Usage = TextureUsage.Sampled,
                    Format = PixelFormat.R32G32B32A32Float,
                    ArrayLayers = (uint)img.Frames.Count,
                }
            );
            foreach (var f in img.Frames) { }
        }
        catch
        {
            t?.Dispose();
        }
        finally
        {
            img?.Dispose();
        }
        if (t == null)
            return;
        resourceSet = parent.Dev.ResourceFactory.CreateResourceSet(
            new() { Layout = parent.Layout, Texture = t }
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
        t?.Dispose();
        t = null;
    }

    public RResourceSet(Stream stream, TResourceSet tr)
    {
        parent = tr;
        if (stream.CanSeek)
            stream.Position = 0;
        ImgStream = new MemoryStream();
        stream.CopyTo(ImgStream);
    }

    private readonly Stream ImgStream;
    public bool EnableMipmap
    {
        get;
        set
        {
            if (value == field)
                return;
            field = value;
            var tmp = IsLoaded;
            Release().GetAwaiter().GetResult();
            if (tmp)
                Load().GetAwaiter().GetResult();
        }
    } = true;
}

public class TResourceSet : ResourceType
{
    internal IGraphicsDevice Dev { get; init; }
    internal IResourceLayout Layout { get; init; }

    public TResourceSet(IGraphicsDevice dev, IResourceLayout layout)
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
    public ITexture Texture { get; init; }
    public IResourceSet ResourceSet { get; init; }

    internal ResourceSetArg(ITexture t, IResourceSet rs)
    {
        Texture = t;
        ResourceSet = rs;
    }
}
