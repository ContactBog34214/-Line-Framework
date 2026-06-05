using System;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using Veldrid;
using static FreeTypeSharp.FT;

public sealed unsafe class FontManager : IDisposable
{
    public float Ascender
    {
        get
        {
            if (_face->size == null)
                return _fontSizeInPixels * 0.8f; // 降级值
            return _face->size->metrics.ascender / 64f;
        }
    }

    public float Descender
    {
        get
        {
            if (_face->size == null)
                return -_fontSizeInPixels * 0.2f; // 降级值
            return _face->size->metrics.descender / 64f;
        }
    }
    
    private FT_LibraryRec_* _library;
    private FT_FaceRec_* _face;
    private readonly GraphicsDevice _gd;
    private uint _fontSizeInPixels = 48;
    private byte[] _fontData;

    /// <summary>
    /// 从文件路径加载字体。
    /// </summary>
    public FontManager(string fontPath, GraphicsDevice graphicsDevice, uint initialSize = 48)
        : this(File.OpenRead(fontPath), graphicsDevice, initialSize) { }

    /// <summary>
    /// 从 Stream 加载字体。
    /// </summary>
    public FontManager(Stream fontStream, GraphicsDevice graphicsDevice, uint initialSize = 48)
    {
        _gd = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        using (var ms = new MemoryStream())
        {
            fontStream.CopyTo(ms);
            _fontData = ms.ToArray();
        }

        FT_LibraryRec_* libraryPtr;
        FT_Error error = FT_Init_FreeType(&libraryPtr);
        if (error != FT_Error.FT_Err_Ok)
            throw new Exception("Failed to initialize FreeType.");
        _library = libraryPtr;

        fixed (byte* pFontData = _fontData)
        {
            FT_FaceRec_* facePtr;
            error = FT_New_Memory_Face(_library, pFontData, (IntPtr)_fontData.Length, 0, &facePtr);
            if (error != FT_Error.FT_Err_Ok)
                throw new Exception("Failed to load font.");
            _face = facePtr;
        }

        SetFontSize(initialSize);
    }

    public void SetFontSize(uint pixelSize)
    {
        if (pixelSize == 0)
            throw new ArgumentOutOfRangeException(nameof(pixelSize));
        _fontSizeInPixels = pixelSize;
        FT_Set_Pixel_Sizes(_face, 0, _fontSizeInPixels);
    }

    /// <summary>
    /// 获取单个字符的灰度纹理（R8_UNorm）。
    /// </summary>
    public Texture GetGlyphTexture(char c)
    {
        uint glyphIndex = FT_Get_Char_Index(_face, c);
        if (glyphIndex == 0)
            return CreateEmptyTexture();

        FT_Error error = FT_Load_Glyph(_face, glyphIndex, 0);
        if (error != FT_Error.FT_Err_Ok)
            return CreateEmptyTexture();

        error = FT_Render_Glyph(_face->glyph, 0);
        if (error != FT_Error.FT_Err_Ok)
            return CreateEmptyTexture();

        FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
        if (bitmap->width == 0 || bitmap->rows == 0)
            return CreateEmptyTexture();

        uint width = bitmap->width;
        uint height = bitmap->rows;
        byte[] pixelData = new byte[width * height];

        for (int row = 0; row < height; row++)
        {
            IntPtr srcPtr = (IntPtr)(bitmap->buffer + row * bitmap->pitch);
            Marshal.Copy(srcPtr, pixelData, row * (int)width, (int)width);
        }

        Texture texture = _gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R8_UNorm,
                TextureUsage.Sampled
            )
        );
        _gd.UpdateTexture(texture, pixelData, 0, 0, 0, width, height, 1, 0, 0);
        return texture;
    }

    /// <summary>
    /// 获取字符的度量信息（用于布局）。
    /// </summary>
    /// <param name="c">字符</param>
    /// <param name="width">渲染后位图宽度（像素）</param>
    /// <param name="height">渲染后位图高度（像素）</param>
    /// <param name="advance">前进量（像素）</param>
    /// <param name="bearingX">水平偏移（像素）</param>
    /// <param name="bearingY">垂直偏移（像素）</param>
    public void GetCharMetrics(
        char c,
        out uint width,
        out uint height,
        out float advance,
        out float bearingX,
        out float bearingY
    )
    {
        uint glyphIndex = FT_Get_Char_Index(_face, c);
        if (glyphIndex == 0)
        {
            width = height = 0;
            advance = bearingX = bearingY = 0;
            return;
        }

        FT_Load_Glyph(_face, glyphIndex, 0);
        FT_Render_Glyph(_face->glyph, 0);

        FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
        width = bitmap->width;
        height = bitmap->rows;
        advance = _face->glyph->advance.x >> 6;
        bearingX = _face->glyph->bitmap_left;
        bearingY = _face->glyph->bitmap_top;
    }

    private Texture CreateEmptyTexture()
    {
        byte[] dummy = new byte[1] { 0 };
        Texture tex = _gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(1, 1, 1, 1, PixelFormat.R8_UNorm, TextureUsage.Sampled)
        );
        _gd.UpdateTexture(tex, dummy, 0, 0, 0, 1, 1, 1, 0, 0);
        return tex;
    }

    public void Dispose()
    {
        if (_face != null)
            FT_Done_Face(_face);
        if (_library != null)
            FT_Done_FreeType(_library);
    }
}
