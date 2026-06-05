using System.Numerics;
using Line.Framework.Graphics;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.UI.DefaultWidget;

public class UIText : UIWidget
{
    public List<Texture> FontTexture { get; private set; } = [];
    private FontManager manager;
    private GraphicsDevice graphic;
    private ResourceLayout resl;
    private List<ResourceSet> rs = [];
    private List<char> Chars = [];
    public List<char> NullChar = [' ', '\n', '\r', '\t'];
    public RgbaFloat color { get; set; } = new(1, 1, 1, 1);
    public string Text
    {
        get => _text;
        set => SetText(value);
    }
    uint _size = 48;
    public uint FontSize
    {
        get => _size;
        set => SetSize(value);
    }
    string _text = "";

    // 字符缓存：存储每个字符的 R8 纹理和度量信息
    private class CharCache
    {
        public Texture R8Texture;
        public uint Width;
        public uint Height;
        public float Advance;
        public float BearingX;
        public float BearingY;
    }

    private Dictionary<char, CharCache> _charCache = new();

    void SetText(string s)
    {
        if (s == _text)
            return;
        _text = s;
        RenderText();
    }

    void SetSize(uint s)
    {
        if (s == _size)
            return;
        _size = s;
        // 字体大小改变后，需要清空缓存并重新渲染
        ClearCharCache();
        RenderText();
    }

    void ClearCharCache()
    {
        foreach (var cache in _charCache.Values)
        {
            cache.R8Texture?.Dispose();
        }
        _charCache.Clear();
    }

    public void RenderText()
    {
        try
        {
            if (manager == null)
                return;
            manager.SetFontSize(_size);

            // 清除之前的资源
            for (; FontTexture.Count != 0; )
            {
                FontTexture[0]?.Dispose();
                FontTexture.RemoveAt(0);
            }
            for (; rs.Count != 0; )
            {
                rs[0]?.Dispose();
                rs.RemoveAt(0);
            }
            Chars.Clear();

            // 遍历每个字符，生成彩色纹理和 ResourceSet
            foreach (char c in _text)
            {
                if (NullChar.Contains(c))
                {
                    FontTexture.Add(null);
                    rs.Add(null);
                    continue;
                }

                // 获取或创建字符的 R8 纹理及度量
                if (!_charCache.TryGetValue(c, out var cache))
                {
                    // 生成 R8 纹理
                    Texture r8Tex = manager.GetGlyphTexture(c);
                    manager.GetCharMetrics(
                        c,
                        out uint w,
                        out uint h,
                        out float adv,
                        out float bx,
                        out float by
                    );
                    cache = new CharCache
                    {
                        R8Texture = r8Tex,
                        Width = w,
                        Height = h,
                        Advance = adv,
                        BearingX = bx,
                        BearingY = by,
                    };
                    _charCache[c] = cache;
                }

                // 将 R8 纹理转换为 RGBA 彩色纹理
                Texture rgbaTex = CreateColoredTextureFromR8Texture(
                    cache.R8Texture,
                    cache.Width,
                    cache.Height,
                    color
                );
                FontTexture.Add(rgbaTex);
                rs.Add(
                    graphic.ResourceFactory.CreateResourceSet(
                        new ResourceSetDescription(resl, rgbaTex)
                    )
                );
            }

            totSize = GetTextSize(_text);
        }
        catch (Exception ex)
        {
            Log.Error($"[FontRenderer] {ex}");
        }
    }

    public void LoadFont(string path)
    {
        manager?.Dispose();
        manager = null;
        ClearCharCache();
        try
        {
            manager = new(path, graphic, FontSize);
            RenderText();
        }
        catch (Exception ex)
        {
            manager?.Dispose();
            Log.Error($"[FontLoader] {ex}");
        }
    }

    public void LoadFont(Stream stream)
    {
        manager?.Dispose();
        manager = null;
        ClearCharCache();
        try
        {
            manager = new(stream, graphic, FontSize);
            RenderText();
        }
        catch (Exception ex)
        {
            manager?.Dispose();
            Log.Error($"[FontLoader] {ex}");
        }
    }

    public UIText(GraphicsDevice gd, ResourceLayout rl)
    {
        graphic = gd;
        resl = rl;
        DisposeHook = () =>
        {
            manager?.Dispose();
            manager = null;
            ClearCharCache();
            for (; FontTexture.Count != 0; )
            {
                FontTexture[0]?.Dispose();
                FontTexture.RemoveAt(0);
            }
            for (; rs.Count != 0; )
            {
                rs[0]?.Dispose();
                rs.RemoveAt(0);
            }
        };

        RendererContext = (args) =>
        {
            var collector = args.Collector;
            void renderAText(Vector2 StartPosition, Vector2 Size, Texture FT, ResourceSet rs)
            {
                var tl = new WindowsRenderer.Vertex(
                    StartPosition,
                    color,
                    new(new(), new(0, 0)),
                    FT,
                    rs,
                    1
                );
                var tr = new WindowsRenderer.Vertex(
                    StartPosition + new Vector2(Size.X, 0),
                    color,
                    new(new(), new(1, 0)),
                    FT,
                    rs,
                    1
                );
                var bl = new WindowsRenderer.Vertex(
                    StartPosition + new Vector2(0, Size.Y),
                    color,
                    new(new(), new(0, 1)),
                    FT,
                    rs,
                    1
                );
                var br = new WindowsRenderer.Vertex(
                    StartPosition + Size,
                    color,
                    new(new(), new(1, 1)),
                    FT,
                    rs,
                    1
                );
                if (FT == null || rs == null)
                    return;
                collector.DrawVertex([tl, tr, bl], this);
                collector.DrawVertex([tr, bl, br], this);
            }
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
                return;

            float ascender = manager?.Ascender ?? _size * 0.8f;
            float lineHeight = _size;
            int totalLines = _text.Split('\n').Length;
            float scaleY = (float)args.height / (lineHeight * totalLines);
            float scaleX = (float)args.width / totSize.X;

            float offset = 0; // 屏幕 X 坐标（已缩放）
            int line = 0;

            for (int i = 0; i < FontTexture.Count; i++)
            {
                char c = _text[i];
                Vector2 charSizeLogical = GetTextSize(c); // 逻辑像素尺寸

                // 处理空格和换行（不渲染但影响布局）
                if (c == ' ')
                {
                    offset += charSizeLogical.X * scaleX * LetterSpacing; // 增加空格占位
                    continue;
                }
                if (c == '\n')
                {
                    line++;
                    offset = 0;
                    continue;
                }
                // 其他控制字符（如 \r, \t）可选择忽略
                if (NullChar.Contains(c))
                    continue;

                // 普通字符：必须已缓存
                if (!_charCache.TryGetValue(c, out var cache))
                    continue;

                float width_screen = charSizeLogical.X * scaleX;
                float height_screen = charSizeLogical.Y * scaleY;

                // 基线对齐计算
                float baselineY_logical = line * lineHeight + ascender;
                float y_logical = baselineY_logical - cache.BearingY;
                float y_screen = y_logical * scaleY;

                renderAText(
                    new Vector2(offset, y_screen),
                    new Vector2(width_screen, height_screen),
                    FontTexture[i],
                    rs[i]
                );

                offset += width_screen * LetterSpacing;
            }
        };
    }

    private Vector2 totSize = new();

    // 从 R8 纹理读取像素数据并生成 RGBA 彩色纹理
    private Texture CreateColoredTextureFromR8Texture(
        Texture r8Texture,
        uint width,
        uint height,
        RgbaFloat textColor
    )
    {
        // 读取 R8 纹理的像素数据
        byte[] grayPixels = ReadPixelsFromR8Texture(r8Texture, width, height);

        // 转换为 RGBA
        byte[] rgbaData = new byte[width * height * 4];
        float r = textColor.R,
            g = textColor.G,
            b = textColor.B,
            a = textColor.A;
        for (int i = 0; i < width * height; i++)
        {
            float intensity = grayPixels[i] / 255.0f;
            byte R = (byte)(r * intensity * 255);
            byte G = (byte)(g * intensity * 255);
            byte B = (byte)(b * intensity * 255);
            byte A = (byte)(a * intensity * 255);
            int offset = i * 4;
            rgbaData[offset + 0] = R;
            rgbaData[offset + 1] = G;
            rgbaData[offset + 2] = B;
            rgbaData[offset + 3] = A;
        }

        Texture rgbaTexture = graphic.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled
            )
        );
        graphic.UpdateTexture(rgbaTexture, rgbaData, 0, 0, 0, width, height, 1, 0, 0);
        return rgbaTexture;
    }

    // 辅助方法：从 R8_UNorm 纹理读取像素数据
    private byte[] ReadPixelsFromR8Texture(Texture texture, uint width, uint height)
    {
        // 创建一个 Staging 纹理用于读取
        Texture staging = graphic.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                width,
                height,
                1,
                1,
                PixelFormat.R8_UNorm,
                TextureUsage.Staging
            )
        );
        CommandList cl = graphic.ResourceFactory.CreateCommandList();
        cl.Begin();
        cl.CopyTexture(texture, staging);
        cl.End();
        graphic.SubmitCommands(cl);
        graphic.WaitForIdle();

        // 映射数据
        MappedResource map = graphic.Map(staging, MapMode.Read);
        byte[] result = new byte[width * height];
        unsafe
        {
            byte* ptr = (byte*)map.Data;
            for (int i = 0; i < width * height; i++)
            {
                result[i] = ptr[i];
            }
        }
        graphic.Unmap(staging);
        staging.Dispose();
        cl.Dispose();
        return result;
    }

    public Vector2 GetTextSize(string s)
    {
        float ThisLineWidth = 0;
        float MaximumWidth = 0;
        foreach (var i in s.ToCharArray())
        {
            if ('\n' == i)
            {
                if (ThisLineWidth > MaximumWidth)
                {
                    MaximumWidth = ThisLineWidth;
                }
                ThisLineWidth = 0;
                continue;
            }
            var tmp = GetTextSize(i);
            ThisLineWidth += tmp.X*LetterSpacing;
        }
        if (ThisLineWidth > MaximumWidth)
        {
            MaximumWidth = ThisLineWidth;
        }
        return new(MaximumWidth, _size * s.Split('\n').Length);
    }

    public Vector2 GetTextSize(char s)
    {
        if (s == ' ')
        {
            return new(_size * SpaceWidth * LetterSpacing, 1);
        }
        else if (NullChar.Contains(s))
        {
            return new(0, 0);
        }

        try
        {
            if (_charCache.TryGetValue(s, out var cache))
            {
                // 使用缓存的度量（位图宽高，与原逻辑一致）
                return new(cache.Width * LetterSpacing, cache.Height);
            }
            else
            {
                // 如果尚未缓存（理论上不会发生，因为 RenderText 已预先生成所有字符）
                manager.GetCharMetrics(s, out uint w, out uint h, out _, out _, out _);
                return new(w * LetterSpacing, h);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[TextRenderer] [Getting size of {s}] {ex}");
            return Vector2.Zero;
        }
    }

    public float SpaceWidth { get; set; } = 0.25f;
    public float LetterSpacing { get; set; } = 1.1f;
}
