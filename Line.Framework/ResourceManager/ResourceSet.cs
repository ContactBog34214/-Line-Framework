using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Line.Framework.Graphics;

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
        try
        {
            ushort metaDataLength = 0;
            ImgStream.Position = 0;
            byte[] ml = new byte[2];
            await ImgStream.ReadExactlyAsync(ml, 0, 2);
            metaDataLength = BinaryPrimitives.ReadUInt16LittleEndian(ml);

            byte[] dataBytes = new byte[metaDataLength];
            ImgStream.Position = 2;
            await ImgStream.ReadExactlyAsync(dataBytes);

            if (!MemoryMarshal.TryRead(dataBytes, out TextureResourceStreamInfo metaData))
                throw new InvalidDataException(
                    $"meta truncated: {metaDataLength} < ${Marshal.SizeOf<TextureResourceStreamInfo>()}"
                );
            ImgStream.Position = 2 + metaDataLength;

            t?.Dispose();
            t = parent.Dev.ResourceFactory.CreateTexture(
                new()
                {
                    ArrayLayers = metaData.ArrayLayers,
                    Format = (PixelFormat)metaData.Format,
                    Width = metaData.Width,
                    Height = metaData.Height,
                    Usage = TextureUsage.Sampled,
                }
            );

            long totLength = ImgStream.Length - ImgStream.Position;

            long bytesPerLayer = totLength / metaData.ArrayLayers;

            for (uint arrayLayer = 0; arrayLayer < metaData.ArrayLayers; arrayLayer++)
            {
                byte[] layerBytes = new byte[bytesPerLayer];
                await ImgStream.ReadExactlyAsync(layerBytes);

                t.Update(layerBytes, arrayLayer: arrayLayer);
            }
        }
        catch
        {
            t?.Dispose();
            t = null;
            throw;
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

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct TextureResourceStreamInfo
{
    [FieldOffset(0)]
    public uint Width = 0;

    [FieldOffset(4)]
    public uint Height = 0;

    [FieldOffset(8)]
    public ushort Format = (ushort)PixelFormat.R8G8B8A8UNorm;

    [FieldOffset(12)]
    public uint ArrayLayers = 1;

    public TextureResourceStreamInfo(uint width, uint height, PixelFormat format, uint arrayLayers)
    {
        Width = width;
        Height = height;
        Format = (ushort)format;
        ArrayLayers = arrayLayers;
    }
}
