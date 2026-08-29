using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using Veldrid;

namespace Line.Framework.Resource.Graphic;

internal sealed class FontBackend : IDisposable
{
    private readonly GraphicsDevice _gd;
    private LunarLabsFonts.Font _font;
    private readonly object _lock = new();
    private bool _disposed;

    public FontBackend(
        Stream fontStream,
        GraphicsDevice graphicsDevice)
    {
        _gd = graphicsDevice
            ?? throw new ArgumentNullException(nameof(graphicsDevice));

        using var ms = new MemoryStream();
        fontStream.CopyTo(ms);

        _font = new LunarLabsFonts.Font(
            ms.ToArray(),
            null);
    }
    public float GetGlyphAdvance(
        char c,
        float pixelSize)
    {
        pixelSize =
            MathF.Max(pixelSize, 1f);

        ThrowIfDisposed();

        float scale =
            _font.ScaleInPixels(pixelSize);

        _font.GetCodepointHMetrics(
            c,
            out int advanceWidth,
            out _);

        return advanceWidth * scale;

    }
    public bool HasGlyph(char c)
    {
        ThrowIfDisposed();

        return _font.HasGlyph(c);
    }
    public FontMetrics GetFontMetrics(float pixelSize)
    {
        pixelSize = MathF.Max(pixelSize, 1f);

        ThrowIfDisposed();

        float scale =
            _font.ScaleInPixels(pixelSize);

        _font.GetFontVMetrics(
            out int ascent,
            out int descent,
            out int lineGap);

        return new FontMetrics
        {
            Scale = scale,

            Ascender =
                ascent * scale,

            Descender =
                descent * scale,

            LineHeight =
                (ascent - descent + lineGap) * scale
        };

    }

    public GlyphBuildResult BuildGlyph(
        char c,
        float pixelSize)
    {
        pixelSize = MathF.Max(pixelSize, 1f);

        float scale;

        lock (_lock)
        {
            ThrowIfDisposed();

            scale =
                _font.ScaleInPixels(pixelSize);
        }

        var bitmap =
            _font
                .RenderGlyph(
                    c,
                    scale,
                    Color.White,
                    Color.Transparent)
                .GetAwaiter()
                .GetResult();

        if (bitmap == null ||
            bitmap.Width <= 0 ||
            bitmap.Height <= 0)
        {
            return new GlyphBuildResult
            {
                Scale = scale
            };
        }

        var metrics =
            _font
                .GetGlyphMetrics(
                    c,
                    scale,
                    scale,
                    0,
                    0)
                .GetAwaiter()
                .GetResult();

        _font.GetCodepointHMetrics(
            c,
            out int advanceWidth,
            out _);

        return new GlyphBuildResult
        {
            Width = (uint)bitmap.Width,
            Height = (uint)bitmap.Height,

            Pixels = bitmap.Pixels,

            Advance =
                advanceWidth * scale,

            BearingX =
                metrics.Bounds.X,

            BearingY =
                metrics.Bounds.Y,

            Scale = scale
        };
    }
    public Texture CreateTexture(
        GlyphBuildResult glyph)
    {
        if (glyph.Width == 0 ||
            glyph.Height == 0)
        {
            return CreateEmptyTexture();
        }


        ThrowIfDisposed();

        var texture =
            _gd.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    glyph.Width,
                    glyph.Height,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));

        _gd.UpdateTexture(
            texture,
            glyph.Pixels,
            0,
            0,
            0,
            glyph.Width,
            glyph.Height,
            1,
            0,
            0);

        return texture;

    }

    private Texture CreateEmptyTexture()
    {
        var texture =
            _gd.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    1,
                    1,
                    1,
                    1,
                    PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));

        byte[] data =
        [
            0,
            0,
            0,
            0
        ];

        _gd.UpdateTexture(
            texture,
            data,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0);

        return texture;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _font == null)
            throw new ObjectDisposedException(
                nameof(FontBackend));
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

internal readonly struct FontMetrics
{
    public float Scale { get; init; }

    public float Ascender { get; init; }

    public float Descender { get; init; }

    public float LineHeight { get; init; }
}

internal sealed class GlyphBuildResult
{
    public uint Width { get; init; }

    public uint Height { get; init; }

    public byte[] Pixels { get; init; }

    public float Advance { get; init; }

    public float BearingX { get; init; }

    public float BearingY { get; init; }

    public float Scale { get; init; }
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

    public RFont(
        GraphicsDevice gd,
        ResourceLayout rl,
        Stream stream)
    {
        dev = gd;
        Layout = rl;

        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            _fontData = ms.ToArray();
        }
    }

    internal class DataPool
    {
        public uint size = 24;

        public List<char> NullChar =
        [
            ' ',
            '\n',
            '\r',
            '\t'
        ];

        public float SpaceWidth = 0.25f;
    }

    public bool IsLoaded => font != null;

    public object GetHandle() => font;

    private CancellationTokenSource tokenSource = new();

    public async Task Load()
    {
        if (IsLoaded)
            return;

        using var ms = new MemoryStream(_fontData);

        tokenSource?.TryReset();

        font = new Font(
            dev,
            ms,
            pool,
            Layout,
            tokenSource.Token);
    }

    public async Task Release()
    {
        if (!IsLoaded)
            return;

        if (tokenSource != null)
            await tokenSource.CancelAsync();

        font?.Dispose();
        font = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Release()
            .GetAwaiter()
            .GetResult();

        _disposed = true;

        tokenSource?.Dispose();
    }
}

public sealed class TFont : ResourceType
{
    GraphicsDevice gd;
    ResourceLayout rl;

    public override async Task<IResource> Create(Stream stream)
    {
        return new RFont(gd, rl, stream);
    }

    public TFont(GraphicsDevice dev, ResourceLayout l)
    {
        gd = dev;
        rl = l;
    }
}

public sealed class Font : IDisposable
{
    private readonly FontBackend backend;
    private readonly RFont.DataPool p;

    private readonly GraphicsDevice Dev;
    private readonly ResourceLayout Layout;

    private readonly ConcurrentDictionary<
        (char Char, int Size),
        FontTexture> TextureCache = new();

    private readonly ConcurrentDictionary<
        (char Char, int Size),
        byte> PendingCharacters = new();

    private readonly ConcurrentQueue<
        (char Char, int Size)> CharQueue = new();

    private readonly CancellationTokenSource
        _buildCancellation = new();

    private readonly Thread BuildThread;

    private bool _disposed;

    public List<char> NullChar => p.NullChar;

    public float SpaceWidth
    {
        get => p.SpaceWidth;
        set => p.SpaceWidth = value;
    }

    internal Font(
        GraphicsDevice dev,
        Stream stream,
        RFont.DataPool pool,
        ResourceLayout layout,
        CancellationToken token)
    {
        Dev = dev;
        Layout = layout;
        p = pool;

        backend = new FontBackend(
            stream,
            dev);

        BuildThread = new Thread(
            () => BuildChar(token))
        {
            IsBackground = true,
            Name = "FontBuildThread"
        };

        BuildThread.Start();
    }

    private static int GetRasterSize(
        float size)
    {
        return Math.Max(
            1,
            (int)MathF.Ceiling(size));
    }

    public float GetAscender(
        float pixelSize)
    {
        return backend
            .GetFontMetrics(pixelSize)
            .Ascender;
    }

    public float GetDescender(
        float pixelSize)
    {
        return backend
            .GetFontMetrics(pixelSize)
            .Descender;
    }

    public float GetLineHeight(
        float pixelSize)
    {
        return backend
            .GetFontMetrics(pixelSize)
            .LineHeight;
    }
    public float GetGlyphAdvance(
    char c,
    float pixelSize)
    {
        return backend.GetGlyphAdvance(
            c,
            pixelSize);
    }

    public void CreateCharTexture(
        char c,
        float size)
    {
        int rasterSize =
            GetRasterSize(size);

        var key =
            (c, rasterSize);

        if (TextureCache.ContainsKey(key))
            return;

        if (!PendingCharacters.TryAdd(
                key,
                0))
        {
            return;
        }

        CharQueue.Enqueue(key);
    }
    public bool HasGlyph(char c) => backend.HasGlyph(c);
    public bool HasCache(
        char c,
        float size)
    {
        int rasterSize =
            GetRasterSize(size);

        return TextureCache.ContainsKey(
            (c, rasterSize));
    }

    public Task<FontTexture> GetFontTexture(
        char c,
        float size)
    {
        size = MathF.Max(size, 1f);

        if (_disposed)
            return Task.FromResult<FontTexture>(null);

        if (NullChar.Contains(c))
        {
            uint width =
                c == ' '
                    ? (uint)MathF.Max(
                        size * SpaceWidth,
                        1f)
                    : 0;

            return Task.FromResult(
                new FontTexture
                {
                    Texture = null,
                    ResourceSet = null,

                    Width = width,
                    Height = (uint)MathF.Ceiling(size),

                    Advance = width,

                    BearingX = 0,
                    BearingY = 0,

                    FontSize = size,

                    Scale = 1f
                });
        }

        int rasterSize =
            GetRasterSize(size);

        var key =
            (c, rasterSize);

        if (TextureCache.TryGetValue(
                key,
                out var cached))
        {
            cached.RequestedSize = size;
            cached.Scale =
                size / rasterSize;

            return Task.FromResult(cached);
        }

        CreateCharTexture(
            c,
            size);

        return WaitForFontTexture(
            c,
            size,
            rasterSize);
    }

    private async Task<FontTexture>
        WaitForFontTexture(
            char c,
            float requestedSize,
            int rasterSize)
    {
        while (!_disposed)
        {
            if (TextureCache.TryGetValue(
                    (c, rasterSize),
                    out var cached))
            {
                cached.RequestedSize =
                    requestedSize;

                cached.Scale =
                    requestedSize / rasterSize;

                return cached;
            }

            await Task.Delay(2);
        }

        return null;
    }

    private void BuildChar(
        CancellationToken externalToken)
    {
        using var linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                externalToken,
                _buildCancellation.Token);

        var token = linked.Token;

        while (!token.IsCancellationRequested)
        {
            if (!CharQueue.TryDequeue(
                    out var request))
            {
                Thread.Sleep(2);
                continue;
            }

            PendingCharacters.TryRemove(
                request,
                out _);

            if (TextureCache.ContainsKey(request))
                continue;

            try
            {
                BuildGlyph(
                    request.Char,
                    request.Size);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Failed to build glyph " +
                    $"'{request.Char}' " +
                    $"size={request.Size}: {ex}");
            }
        }
    }

    private void BuildGlyph(
        char c,
        int rasterSize)
    {
        var key =
            (c, rasterSize);

        if (TextureCache.ContainsKey(key))
            return;

        // 注意这里：
        // bitmap 真正以整数 rasterSize 生成。
        var glyph =
            backend.BuildGlyph(
                c,
                rasterSize);

        if (glyph == null)
            return;

        if (glyph.Width == 0 ||
            glyph.Height == 0)
        {
            TextureCache.TryAdd(
                key,
                new FontTexture
                {
                    Texture = null,
                    ResourceSet = null,

                    Width = 0,
                    Height = 0,

                    Advance = 0,
                    BearingX = 0,
                    BearingY = 0,

                    FontSize = rasterSize,
                    RequestedSize = rasterSize,
                    Scale = 1f
                });

            return;
        }

        Texture texture =
            backend.CreateTexture(glyph);

        ResourceSet resourceSet =
            Dev.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(
                    Layout,
                    texture));

        var cache =
            new FontTexture
            {
                Texture = texture,
                ResourceSet = resourceSet,

                Width = glyph.Width,
                Height = glyph.Height,

                Advance = glyph.Advance,
                BearingX = glyph.BearingX,
                BearingY = glyph.BearingY,

                FontSize = rasterSize,
                RequestedSize = rasterSize,

                Scale = 1f
            };

        if (!TextureCache.TryAdd(
                key,
                cache))
        {
            cache.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _buildCancellation.Cancel();

        if (BuildThread.IsAlive &&
            !ReferenceEquals(
                Thread.CurrentThread,
                BuildThread))
        {
            BuildThread.Join();
        }

        foreach (var item in TextureCache)
            item.Value?.Dispose();

        TextureCache.Clear();

        PendingCharacters.Clear();

        CharQueue.Clear();

        backend.Dispose();

        _buildCancellation.Dispose();
    }
}

public sealed class FontTexture : IDisposable
{
    public Texture Texture { get; set; }

    public ResourceSet ResourceSet { get; set; }

    // 原始 raster bitmap 尺寸
    public uint Width { get; set; }

    public uint Height { get; set; }

    // 原始 raster 尺寸下的 metrics
    public float Advance { get; set; }

    public float BearingX { get; set; }

    public float BearingY { get; set; }

    // 实际生成 bitmap 的字号
    public float FontSize { get; init; }

    // UIText 当前请求的字号
    public float RequestedSize { get; set; }

    // RequestedSize / FontSize
    public float Scale { get; set; }

    public void Dispose()
    {
        ResourceSet?.Dispose();
        Texture?.Dispose();

        ResourceSet = null;
        Texture = null;
    }
}