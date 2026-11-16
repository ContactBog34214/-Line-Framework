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
                    // EnableMipmap 默认 true，必须在这里落实成实际的 mip 层数：
                    // 留 0 会被两个后端归一化成 1，等于关掉 mip 链，
                    // 而 EnableMipmap 的 setter 只是重新加载同一份单层纹理，
                    // 视觉上只剩缩小采样的锯齿（无 mip 时远处会闪烁）。
                    //
                    // 上限按尺寸钳制：mip 层数不能超过 1 + log2(最长边)。
                    // 例如 1x1 的小图最多 1 层，硬写 4 会被后端拒绝（或创建失败）。
                    MipLevels = EnableMipmap
                        ? Math.Min(DefaultMipLevels, MaxMipLevels(metaData.Width, metaData.Height))
                        : 1u,
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

    /// <summary>
    /// 启用 mipmap 时为纹理分配的 mip 层数。
    /// <para>
    /// 注意：加载器目前<b>只上传第 0 层</b>（源文件里只有基础层数据），
    /// 第 1 层及以下是创建后未写入的显存。采样器若真的落到这些层，
    /// 读到的是未定义内容（可能全黑、也可能随机色）。
    /// </para>
    /// <para>
    /// 之所以仍取 4 而不是「按尺寸自动算」（如 1024 → 11 层）：
    /// 层数越多，未初始化层的面积占比越大；4 层在缩小一半以内时
    /// 仍主要由第 0、1 层决定，风险较小。要真正解决问题需要实现
    /// 降采样生成 mip 链（CPU 端逐级 box filter 后逐层 Update）。
    /// </para>
    /// </summary>
    public const uint DefaultMipLevels = 4;

    /// <summary>
    /// 给定尺寸允许的最大 mip 层数（1 + floor(log2(最长边))）。
    /// <para>
    /// 用于把 <see cref="DefaultMipLevels"/> 钳制到该纹理真实能达到的层数：
    /// 小图硬写 4 层会超出限制，后端会拒绝创建。
    /// </para>
    /// </summary>
    private static uint MaxMipLevels(uint width, uint height)
    {
        uint longest = Math.Max(width, height);
        if (longest == 0)
            return 1;

        uint levels = 1;
        while (longest > 1)
        {
            longest >>= 1;
            levels++;
        }

        return levels;
    }

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
