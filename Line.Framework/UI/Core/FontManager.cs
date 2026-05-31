using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FreeTypeSharp;
using Veldrid;
using static FreeTypeSharp.FT;

public sealed unsafe class FontManager : IDisposable
{
    private FT_LibraryRec_* _library;
    private FT_FaceRec_* _face;
    private readonly GraphicsDevice _gd;
    private uint _fontSizeInPixels = 48;
    private uint _currentFontSizePx = 48;
    private byte[] _fontData; // 用于从 Stream 加载时保存字体数据，确保它不被垃圾回收

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

        // 1. 将 Stream 内容读取到字节数组中
        using (var memoryStream = new MemoryStream())
        {
            fontStream.CopyTo(memoryStream);
            _fontData = memoryStream.ToArray(); // 存储起来保证 pinned object 的生命周期
        }

        // 2. 初始化 FreeType 库
        FT_LibraryRec_* libraryPtr;
        FT_Error error = FT_Init_FreeType(&libraryPtr);
        if (error != FT_Error.FT_Err_Ok)
            throw new Exception("Failed to initialize FreeType library.");
        _library = libraryPtr;

        // 3. 固定字节数组，获取指针并加载字体 face
        fixed (byte* pFontData = _fontData)
        {
            FT_FaceRec_* facePtr;
            error = FT_New_Memory_Face(_library, pFontData, (IntPtr)_fontData.Length, 0, &facePtr);
            if (error != FT_Error.FT_Err_Ok)
                throw new Exception("Failed to load font from stream.");
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

    public Texture GetGlyphTexture(char c)
    {
        uint glyphIndex = FT_Get_Char_Index(_face, c);
        if (glyphIndex == 0)
            throw new InvalidOperationException($"Glyph not found for character '{c}'.");

        FT_Error error = FT_Load_Glyph(_face, glyphIndex, 0); // FT_LOAD_DEFAULT = 0
        if (error != FT_Error.FT_Err_Ok)
            throw new Exception("Failed to load glyph.");

        error = FT_Render_Glyph(_face->glyph, 0); // FT_RENDER_MODE_NORMAL = 0
        if (error != FT_Error.FT_Err_Ok)
            throw new Exception("Failed to render glyph.");

        FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
        if (bitmap->width == 0 || bitmap->rows == 0)
            return CreateEmptyTexture();

        uint width = bitmap->width;
        uint rows = bitmap->rows;
        byte[] pixelData = new byte[width * rows];

        for (int row = 0; row < rows; row++)
        {
            IntPtr srcPtr = (IntPtr)(bitmap->buffer + row * bitmap->pitch);
            Marshal.Copy(srcPtr, pixelData, row * (int)width, (int)width);
        }

        Texture texture = _gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                rows,
                1,
                1,
                PixelFormat.R8_UNorm,
                TextureUsage.Sampled
            )
        );
        _gd.UpdateTexture(texture, pixelData, 0, 0, 0, width, rows, 1, 0, 0);
        return texture;
    }

    public Texture GetTextTexture(string text)
    {
        if (string.IsNullOrEmpty(text))
            return CreateEmptyTexture();

        var glyphInfos = new List<GlyphLayoutInfo>();
        int totalWidth = 0;
        int maxHeight = 0;
        int baselineY = 0;

        foreach (char c in text)
        {
            uint glyphIndex = FT_Get_Char_Index(_face, c);
            if (glyphIndex == 0)
                continue;

            FT_Load_Glyph(_face, glyphIndex, 0);
            FT_Render_Glyph(_face->glyph, 0);

            FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
            int bitmapLeft = _face->glyph->bitmap_left;
            int bitmapTop = _face->glyph->bitmap_top;
            int advance = (int)(_face->glyph->advance.x >> 6);

            glyphInfos.Add(
                new GlyphLayoutInfo
                {
                    Bitmap = bitmap,
                    BitmapLeft = bitmapLeft,
                    BitmapTop = bitmapTop,
                    Advance = advance,
                    Width = (int)bitmap->width,
                    Height = (int)bitmap->rows,
                }
            );

            totalWidth += advance;
            int neededHeight = bitmapTop + (int)bitmap->rows;
            if (neededHeight > maxHeight)
                maxHeight = neededHeight;
            if (bitmapTop > baselineY)
                baselineY = bitmapTop;
        }

        if (glyphInfos.Count == 0)
            return CreateEmptyTexture();

        byte[] fullBuffer = new byte[totalWidth * maxHeight];
        Array.Fill(fullBuffer, (byte)0);

        int penX = 0;
        foreach (var info in glyphInfos)
        {
            if (info.Width == 0 || info.Height == 0)
                continue;

            int destX = penX + info.BitmapLeft;
            int destY = baselineY - info.BitmapTop;

            for (int row = 0; row < info.Height; row++)
            {
                IntPtr srcRowPtr = (IntPtr)(info.Bitmap->buffer + row * info.Bitmap->pitch);
                int destOffset = (destY + row) * totalWidth + destX;
                if (destOffset + info.Width > fullBuffer.Length)
                    continue;
                Marshal.Copy(srcRowPtr, fullBuffer, destOffset, info.Width);
            }

            penX += info.Advance;
        }

        Texture texture = _gd.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                (uint)totalWidth,
                (uint)maxHeight,
                1,
                1,
                PixelFormat.R8_UNorm,
                TextureUsage.Sampled
            )
        );
        _gd.UpdateTexture(texture, fullBuffer, 0, 0, 0, (uint)totalWidth, (uint)maxHeight, 1, 0, 0);
        return texture;
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

    /// <summary>
    /// 获取文本的灰度像素数据（R8 格式），不创建纹理。
    /// </summary>
    /// <returns>(pixelData, width, height)</returns>
    public (byte[] pixels, uint width, uint height) GetTextPixels(string text)
    {
        if (string.IsNullOrEmpty(text))
            return (new byte[1] { 0 }, 1, 1);

        // 复用 GetTextTexture 中的布局逻辑，但不创建纹理
        var glyphInfos = new List<GlyphLayoutInfo>();
        int totalWidth = 0;
        int maxHeight = 0;
        int baselineY = 0;

        foreach (char c in text)
        {
            uint glyphIndex = FT_Get_Char_Index(_face, c);
            if (glyphIndex == 0)
                continue;

            FT_Load_Glyph(_face, glyphIndex, 0);
            FT_Render_Glyph(_face->glyph, 0);

            FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
            int bitmapLeft = _face->glyph->bitmap_left;
            int bitmapTop = _face->glyph->bitmap_top;
            int advance = (int)(_face->glyph->advance.x >> 6);

            glyphInfos.Add(
                new GlyphLayoutInfo
                {
                    Bitmap = bitmap,
                    BitmapLeft = bitmapLeft,
                    BitmapTop = bitmapTop,
                    Advance = advance,
                    Width = (int)bitmap->width,
                    Height = (int)bitmap->rows,
                }
            );

            totalWidth += advance;
            int neededHeight = bitmapTop + (int)bitmap->rows;
            if (neededHeight > maxHeight)
                maxHeight = neededHeight;
            if (bitmapTop > baselineY)
                baselineY = bitmapTop;
        }

        if (glyphInfos.Count == 0)
            return (new byte[1] { 0 }, 1, 1);

        byte[] fullBuffer = new byte[totalWidth * maxHeight];
        Array.Fill(fullBuffer, (byte)0);

        int penX = 0;
        foreach (var info in glyphInfos)
        {
            if (info.Width == 0 || info.Height == 0)
                continue;

            int destX = penX + info.BitmapLeft;
            int destY = baselineY - info.BitmapTop;

            for (int row = 0; row < info.Height; row++)
            {
                IntPtr srcRowPtr = (IntPtr)(info.Bitmap->buffer + row * info.Bitmap->pitch);
                int destOffset = (destY + row) * totalWidth + destX;
                if (destOffset + info.Width > fullBuffer.Length)
                    continue;
                Marshal.Copy(srcRowPtr, fullBuffer, destOffset, info.Width);
            }

            penX += info.Advance;
        }

        return (fullBuffer, (uint)totalWidth, (uint)maxHeight);
    }

    public (uint width, uint height) GetTextSize(string text, uint? fontSize = null)
    {
        bool needRestore = false;
        uint previousSize = _currentFontSizePx;

        if (fontSize.HasValue && fontSize.Value != _currentFontSizePx)
        {
            FT_Set_Pixel_Sizes(_face, 0, fontSize.Value);
            _currentFontSizePx = fontSize.Value;
            needRestore = true;
        }

        try
        {
            if (string.IsNullOrEmpty(text))
                return (0, 0);

            int totalWidth = 0;
            int maxHeight = 0;
            int baselineY = 0;

            foreach (char c in text)
            {
                uint glyphIndex = FT_Get_Char_Index(_face, c);
                if (glyphIndex == 0)
                    continue;

                FT_Load_Glyph(_face, glyphIndex, 0);
                FT_Render_Glyph(_face->glyph, 0);

                FT_Bitmap_* bitmap = &(_face->glyph->bitmap);
                int bitmapTop = _face->glyph->bitmap_top;
                int advance = (int)(_face->glyph->advance.x >> 6);

                totalWidth += advance;
                int neededHeight = bitmapTop + (int)bitmap->rows;
                if (neededHeight > maxHeight)
                    maxHeight = neededHeight;
                if (bitmapTop > baselineY)
                    baselineY = bitmapTop;
            }

            // 最终高度 = 基线以上最大部分 + 基线以下最大部分
            int finalHeight = baselineY + (maxHeight - baselineY);
            return ((uint)totalWidth, (uint)finalHeight);
        }
        finally
        {
            if (needRestore)
            {
                FT_Set_Pixel_Sizes(_face, 0, previousSize);
                _currentFontSizePx = previousSize;
            }
        }
    }

    public void Dispose()
    {
        if (_face != null)
            FT_Done_Face(_face);
        if (_library != null)
            FT_Done_FreeType(_library);
    }

    private struct GlyphLayoutInfo
    {
        public FT_Bitmap_* Bitmap;
        public int BitmapLeft;
        public int BitmapTop;
        public int Advance;
        public int Width;
        public int Height;
    }
}
