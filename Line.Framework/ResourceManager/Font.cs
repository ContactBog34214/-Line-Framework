using Veldrid;

namespace Line.Framework.Resource.Graphic;

internal sealed class FontBackend : IDisposable
{
    private readonly GraphicsDevice _gd;
    private LunarLabs.Fonts.Font _font;
    private float _fontScale;
    private uint _fontSizeInPixels;
    private readonly object _lock = new object();
    private bool _disposed;

    private float _ascender;
    private float _descender;
    private float _lineHeight;

    public float Ascender => _ascender;
    public float Descender => _descender;
    public float LineHeight => _lineHeight;

    /// <summary>
    /// 将单通道灰度数据转换为RGBA格式
    /// </summary>
    /// <param name="grayscaleData">灰度数据（0-255）</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <param name="color">要渲染的文字颜色</param>
    /// <returns>RGBA格式的像素数据</returns>
    private byte[] ConvertToRGBA(byte[] grayscaleData, int width, int height, RgbaFloat color)
    {
        byte[] rgbaData = new byte[width * height * 4];

        for (int i = 0; i < grayscaleData.Length; i++)
        {
            byte alpha = grayscaleData[i]; // 灰度值直接作为透明度
            byte r = (byte)(color.R * 255);
            byte g = (byte)(color.G * 255);
            byte b = (byte)(color.B * 255);

            rgbaData[i * 4 + 0] = r; // R
            rgbaData[i * 4 + 1] = g; // G
            rgbaData[i * 4 + 2] = b; // B
            rgbaData[i * 4 + 3] = alpha; // A
        }

        return rgbaData;
    }

    public FontBackend(Stream fontStream, GraphicsDevice graphicsDevice, uint initialSize = 48)
    {
        _gd = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        using var ms = new MemoryStream();
        fontStream.CopyTo(ms);
        byte[] fontData = ms.ToArray();

        _font = new LunarLabs.Fonts.Font(fontData);
        SetFontSize(initialSize);
    }

    public void SetFontSize(uint pixelSize)
    {
        if (pixelSize == 0)
            throw new ArgumentOutOfRangeException(nameof(pixelSize));

        lock (_lock)
        {
            if (_disposed)
                return;
            _fontSizeInPixels = pixelSize;
            _fontScale = _font.ScaleInPixels(pixelSize);

            var refResult = _font.RenderGlyph('A', _fontScale);
            if (refResult != null)
            {
                // yOfs = distance from baseline to top of glyph (positive)
                _ascender = refResult.yOfs;
                var gResult = _font.RenderGlyph('g', _fontScale);
                if (gResult != null)
                {
                    // Descender = baseline to bottom = yOfs - height
                    _descender = gResult.yOfs - gResult.Image.Height;
                }
                else
                {
                    _descender = -pixelSize * 0.2f;
                }
                _lineHeight = _ascender - _descender;
            }
            else
            {
                _ascender = pixelSize * 0.8f;
                _descender = -pixelSize * 0.2f;
                _lineHeight = pixelSize;
            }
        }
    }

    public Texture GetGlyphTexture(char c)
    {
        lock (_lock)
        {
            if (_disposed)
                return CreateEmptyTexture();

            var result = _font.RenderGlyph(c, _fontScale);
            if (result == null || result.Image.Width == 0 || result.Image.Height == 0)
                return CreateEmptyTexture();

            uint width = (uint)result.Image.Width;
            uint height = (uint)result.Image.Height;
            byte[] pixelData = ConvertToRGBA(
                result.Image.Pixels,
                (int)width,
                (int)height,
                new(1, 1, 1, 1)
            );

            Texture texture = _gd.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    width,
                    height,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled
                )
            );
            _gd.UpdateTexture(texture, pixelData, 0, 0, 0, width, height, 1, 0, 0);
            return texture;
        }
    }

    public void GetCharMetrics(
        char c,
        out uint width,
        out uint height,
        out float advance,
        out float bearingX,
        out float bearingY
    )
    {
        lock (_lock)
        {
            if (_disposed)
            {
                width = height = 0;
                advance = bearingX = bearingY = 0;
                return;
            }

            var result = _font.RenderGlyph(c, _fontScale);
            if (result == null || result.Image.Width == 0 || result.Image.Height == 0)
            {
                width = height = 0;
                advance = bearingX = bearingY = 0;
                return;
            }

            width = (uint)result.Image.Width;
            height = (uint)result.Image.Height;
            advance = result.xAdvance;
            bearingX = result.xOfs;
            bearingY = result.yOfs;
        }
    }

    private Texture CreateEmptyTexture()
    {
        byte[] dummy = { 0, 0, 0, 0 }; // 修改为RGBA空纹理的默认数据
        Texture tex = _gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                1,
                1,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled
            )
        );
        _gd.UpdateTexture(tex, dummy, 0, 0, 0, 1, 1, 1, 0, 0);
        return tex;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _font = null;
            _disposed = true;
        }
    }
}

// ---------- 以下类保持不变（已正确适配）----------
public sealed class RFont : IResource
{
    Font font;
    DataPool pool = new();
    ResourceLayout Layout;
    byte[] _fontData;
    GraphicsDevice dev;
    bool _disposed = false;

    public RFont(GraphicsDevice gd, ResourceLayout rl, Stream stream)
    {
        dev = gd;
        Layout = rl;
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            _fontData = ms.ToArray();
        }
    }

    uint Size
    {
        get => pool.size;
        set { pool.size = value; }
    }

    internal class DataPool
    {
        public uint size = 24;
        public List<char> NullChar = [' ', '\n', '\r', '\t'];
        public float SpaceWidth = 0.25f;
    }

    public bool IsLoaded => font != null;

    public object GetHandle() => font;

    public void Load()
    {
        if (IsLoaded)
            return;
        using (var ms = new MemoryStream(_fontData))
        {
            font = new Font(dev, ms, pool, Layout);
        }
    }

    public void Release()
    {
        if (!IsLoaded)
            return;
        font?.Dispose();
        font = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Release();
        _disposed = true;
    }
}

public sealed class TFont : ResourceType
{
    GraphicsDevice gd;
    ResourceLayout rl;

    public override void Create(string id, Stream stream)
    {
        var t = new RFont(gd, rl, stream);
        Manager.AddResource(id, t);
    }

    public TFont(ResourceManager rm, GraphicsDevice dev, ResourceLayout l)
        : base(rm)
    {
        gd = dev;
        rl = l;
    }
}

public sealed class Font : IDisposable
{
    private FontBackend backend;
    private RFont.DataPool p;
    private uint _size = 0;
    private GraphicsDevice Dev;
    private ResourceLayout Layout;
    private readonly object _cacheLock = new object();
    private Dictionary<char, FontTexture> TextureCache = new();

    public uint Size
    {
        get => p.size;
        set
        {
            lock (_cacheLock)
                p.size = value;
        }
    }

    public float Ascender => backend.Ascender;
    public float Descender => backend.Descender;
    public float LineHeight => backend.LineHeight;
    public List<char> NullChar => p.NullChar;
    public float SpaceWidth
    {
        get => p.SpaceWidth;
        set => p.SpaceWidth = value;
    }

    internal Font(GraphicsDevice dev, Stream stream, RFont.DataPool pool, ResourceLayout layout)
    {
        Dev = dev;
        Layout = layout;
        backend = new FontBackend(stream, dev, pool.size);
        p = pool;
        _size = pool.size;
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            backend?.Dispose();
            foreach (var kv in TextureCache)
                kv.Value?.Dispose();
            TextureCache.Clear();
        }
    }

    public FontTexture GetFontTexture(char c)
    {
        if (NullChar.Contains(c))
        {
            uint w = c == ' ' ? _size * (uint)SpaceWidth : 0;
            return new FontTexture
            {
                Texture = null,
                ResourceSet = null,
                Width = w,
                Height = _size,
                Advance = 0,
                BearingX = 0,
                BearingY = 0,
            };
        }

        lock (_cacheLock)
        {
            if (_size != p.size)
            {
                foreach (var kv in TextureCache)
                    kv.Value?.Dispose();
                TextureCache.Clear();
                backend.SetFontSize(p.size);
                _size = p.size;
            }

            if (TextureCache.TryGetValue(c, out var cached))
                return cached;

            Texture r8Tex = backend.GetGlyphTexture(c);
            backend.GetCharMetrics(
                c,
                out uint w,
                out uint h,
                out float adv,
                out float bx,
                out float by
            );
            var rs = Dev.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(Layout, r8Tex)
            );
            var cache = new FontTexture
            {
                Texture = r8Tex,
                ResourceSet = rs,
                Width = w,
                Height = h,
                Advance = adv,
                BearingX = bx,
                BearingY = by,
            };
            TextureCache.Add(c, cache);
            return cache;
        }
    }
}

public class FontTexture : IDisposable
{
    public Texture Texture { get; set; }
    public ResourceSet ResourceSet { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public float Advance { get; set; }
    public float BearingX { get; set; }
    public float BearingY { get; set; }

    public void Dispose()
    {
        Texture?.Dispose();
        ResourceSet?.Dispose();
    }
}
