using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridTexture : ITexture
{
    internal readonly global::Veldrid.Texture texture;
    public uint Width => texture.Width;

    public uint Height => texture.Height;

    public uint ArrayLayers => texture.ArrayLayers;

    public uint MipLevels => texture.MipLevels;

    public uint Depth => texture.Depth;

    public TextureType Type { get; }

    public PixelFormat Format { get; }

    public SampleCount SampleCount { get; }

    public TextureUsage Usage { get; }

    public string Name => texture.Name;

    internal VeldridTexture(global::Veldrid.Texture source)
    {
        texture = source;
        Type = VeldridConverter.ConvertTextureType(source.Type);
        Format = VeldridConverter.ConvertPixelFormat(source.Format);
        SampleCount = VeldridConverter.ConvertSampleCount(source.SampleCount);
        Usage = VeldridConverter.ConvertTextureUsage(source.Usage);
    }

    public void Dispose()
    {
        texture?.Dispose();
    }
}

public partial class VeldridResourceFactory
{
    public ITexture CreateTexture(TextureCreateInfo createInfo)
    {
        var texture = dev.ResourceFactory.CreateTexture(
            VeldridConverter.ConvertTextureDescription(createInfo)
        );
        try
        {
            return new VeldridTexture(texture);
        }
        catch
        {
            texture?.Dispose();
            throw;
        }
    }
}

public partial class VeldridConverter
{
    public static global::Veldrid.TextureDescription ConvertTextureDescription(
        TextureCreateInfo createInfo
    )
    {
        if (createInfo.Width == 0)
            throw new ArgumentException(
                "Texture width must be non-zero. TextureCreateInfo is a plain struct, "
                    + "so forgetting to set Width leaves it at 0.",
                nameof(createInfo)
            );

        // TextureCreateInfo 是纯 struct，未赋值的字段都是 0，
        // 而 Veldrid 要求 Width / Height / Depth / ArrayLayers / MipLevels 全部 >= 1
        // （否则抛 "Width, Height, and Depth must be non-zero"）。
        // 这里把「0 = 未指定」统一归一化为 1，调用方只需关心真正想改的字段。
        //
        // TextureType 同理：枚举从 Texture1D = 1 起、没有 0 值，
        // 未赋值时按最常见情形取 Texture2D。
        var type = createInfo.Type == 0 ? TextureType.Texture2D : createInfo.Type;

        return new()
        {
            ArrayLayers = createInfo.ArrayLayers == 0 ? 1u : createInfo.ArrayLayers,
            Depth = createInfo.Depth == 0 ? 1u : createInfo.Depth,
            Format = ConvertPixelFormat(createInfo.Format),
            Height = createInfo.Height == 0 ? 1u : createInfo.Height,
            MipLevels = createInfo.MipLevels == 0 ? 1u : createInfo.MipLevels,
            SampleCount = ConvertSampleCount(createInfo.SampleCount),
            Type = ConvertTextureType(type),
            Usage = ConvertTextureUsage(createInfo.Usage),
            Width = createInfo.Width,
        };
    }

    public static TextureCreateInfo ConvertTextureDescription(
        global::Veldrid.TextureDescription des
    )
    {
        return new()
        {
            ArrayLayers = des.ArrayLayers,
            Depth = des.Depth,
            Format = ConvertPixelFormat(des.Format),
            Height = des.Height,
            MipLevels = des.MipLevels,
            SampleCount = ConvertSampleCount(des.SampleCount),
            Type = ConvertTextureType(des.Type),
            Usage = ConvertTextureUsage(des.Usage),
            Width = des.Width,
        };
    }

    public static global::Veldrid.TextureType ConvertTextureType(TextureType type)
    {
        return type switch
        {
            TextureType.Texture1D => global::Veldrid.TextureType.Texture1D,

            TextureType.Texture2D => global::Veldrid.TextureType.Texture2D,

            TextureType.Texture3D => global::Veldrid.TextureType.Texture3D,

            _ => throw new NotSupportedException($"Unsupported texture type: {type}"),
        };
    }

    public static TextureType ConvertTextureType(global::Veldrid.TextureType type)
    {
        return type switch
        {
            global::Veldrid.TextureType.Texture1D => TextureType.Texture1D,

            global::Veldrid.TextureType.Texture2D => TextureType.Texture2D,

            global::Veldrid.TextureType.Texture3D => TextureType.Texture3D,

            _ => throw new NotSupportedException($"Unsupported Veldrid texture type: {type}"),
        };
    }

    public static global::Veldrid.TextureUsage ConvertTextureUsage(TextureUsage usage)
    {
        global::Veldrid.TextureUsage result = 0;

        if ((usage & TextureUsage.Sampled) != 0)
            result |= global::Veldrid.TextureUsage.Sampled;

        if ((usage & TextureUsage.ColorTarget) != 0)
            result |= global::Veldrid.TextureUsage.RenderTarget;

        if ((usage & TextureUsage.DepthStencilTarget) != 0)
            result |= global::Veldrid.TextureUsage.DepthStencil;

        if ((usage & TextureUsage.StorageRead) != 0 || (usage & TextureUsage.StorageWrite) != 0)
            result |= global::Veldrid.TextureUsage.Storage;

        return result;
    }

    public static TextureUsage ConvertTextureUsage(global::Veldrid.TextureUsage usage)
    {
        TextureUsage result = 0;

        if ((usage & global::Veldrid.TextureUsage.Sampled) != 0)
            result |= TextureUsage.Sampled;

        if ((usage & global::Veldrid.TextureUsage.RenderTarget) != 0)
            result |= TextureUsage.ColorTarget;

        if ((usage & global::Veldrid.TextureUsage.DepthStencil) != 0)
            result |= TextureUsage.DepthStencilTarget;

        if ((usage & global::Veldrid.TextureUsage.Storage) != 0)
        {
            result |= TextureUsage.StorageRead;
            result |= TextureUsage.StorageWrite;
        }

        return result;
    }
}
