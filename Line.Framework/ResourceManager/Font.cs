using System.Drawing;
using System.Numerics;
using Veldrid;

namespace Line.Framework.Resource.Graphic;

internal sealed class FontBackend : IDisposable
{
    private readonly GraphicsDevice _gd;
    private LunarLabsFonts.Font _font;
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

    public FontBackend(Stream fontStream, GraphicsDevice graphicsDevice, uint initialSize = 48)
    {
        _gd = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        using var ms = new MemoryStream();
        fontStream.CopyTo(ms);
        byte[] fontData = ms.ToArray();

        _font = new LunarLabsFonts.Font(fontData, null);
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

            var metricsA = _font
                .GetGlyphMetrics('A', _fontScale, _fontScale, 0, 0)
                .GetAwaiter()
                .GetResult();
            // yOfs = distance from baseline to top of glyph (positive)
            _ascender = metricsA.Bounds.Y;

            var metricsG = _font
                .GetGlyphMetrics('g', _fontScale, _fontScale, 0, 0)
                .GetAwaiter()
                .GetResult();
            // Descender = baseline to bottom = yOfs - height
            _descender =
                metricsG.Bounds.Height > 0
                    ? metricsG.Bounds.Y - metricsG.Bounds.Height
                    : -pixelSize * 0.2f;
            _lineHeight = _ascender - _descender;
        }
    }

    public async Task<Texture> GetGlyphTexture(char c)
    {
        if (_disposed)
            return CreateEmptyTexture();

        var result = await _font.RenderGlyph(c, _fontScale, Color.White, Color.Transparent);
        if (result == null || result.Width == 0 || result.Height == 0)
            return CreateEmptyTexture();

        uint width = (uint)result.Width;
        uint height = (uint)result.Height;
        byte[] pixelData = result.Pixels;
        lock (_lock)
        {
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

            var result = _font
                .RenderGlyph(c, _fontScale, Color.White, Color.Transparent)
                .GetAwaiter()
                .GetResult();
            if (result == null || result.Width == 0 || result.Height == 0)
            {
                width = height = 0;
                advance = bearingX = bearingY = 0;
                return;
            }

            width = (uint)result.Width;
            height = (uint)result.Height;

            var metrics = _font
                .GetGlyphMetrics(c, _fontScale, _fontScale, 0, 0)
                .GetAwaiter()
                .GetResult();
            _font.GetCodepointHMetrics(c, out int advanceWidth, out _);
            advance = (int)Math.Floor(advanceWidth * _fontScale);
            bearingX = metrics.Bounds.X;
            bearingY = metrics.Bounds.Y;
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

    public async Task Load()
    {
        if (IsLoaded)
            return;
        using (var ms = new MemoryStream(_fontData))
        {
            font = new Font(dev, ms, pool, Layout);
        }
    }

    public async Task Release()
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
        Release().GetAwaiter().GetResult();
        _disposed = true;
    }
}

public sealed class TFont : ResourceType
{
    GraphicsDevice gd;
    ResourceLayout rl;

    public override async Task Create(string id, Stream stream)
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
            TextureCache?.Clear();
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

            Texture r8Tex = backend.GetGlyphTexture(c).GetAwaiter().GetResult();
            backend.GetCharMetrics(
                c,
                out uint w,
                out uint h,
                out float adv,
                out float bx,
                out float by
            );
            var rs = Dev?.ResourceFactory.CreateResourceSet(
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
