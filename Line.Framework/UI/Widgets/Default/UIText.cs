using System.Dynamic;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Graphic;
using TagLib.IFD.Entries;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.UI.DefaultWidget;

public sealed class UIText : UIWidget
{
    public List<Texture> FontTexture { get; private set; } = [];
    public string FontId { get; set; }
    public List<char> NullChar
    {
        get => (rm.GetResource(FontId) as Font)?.NullChar ?? [];
    }
    public RgbaFloat color { get; set; } = new(1, 1, 1, 1);
    public string Text
    {
        get => _text;
        set => SetText(value);
    }
    string _text = "";
    public float FontScale { get; set; } = 1;

    private readonly Dictionary<char, FontTexture> _charCache = new();

    public override void RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    void SetText(string s)
    {
        if (s == _text)
            return;
        _text = s;
    }

    readonly Action<RendererContextArgs> RenderAction;

    public UIText(ResourceManager manager)
    {
        rm = manager;

        RenderAction = (args) =>
        {
            using var nullWriter = new StreamWriter(Stream.Null);
            var collector = args.Collector;
            if (manager == null)
                return;
            var font = rm.GetResource(FontId) as Font;
            if (font == null)
                return;

            // ---------- 坐标系配置 ----------
            // 如果屏幕 Y 轴向下为正（左上角原点），设为 true；Y 轴向上为正（左下角原点），设为 false

            // 基线起始位置（屏幕坐标）
            float lineHeight = font.Size / 1.4f;
            Vector2 baselinePos = new Vector2(0, 0);
            var s = _text.Split('\n');

            baselinePos.Y = -font.Ascender * FontScale;
            if (YAlignment == Alignment.Center)
                baselinePos.Y += ((float)args.height - s.Length * lineHeight * FontScale) / 2f;
            if (YAlignment == Alignment.Right)
                baselinePos.Y += (float)args.height - s.Length * lineHeight * FontScale;
            void ResetOffset(string str)
            {
                if (XAlignment == Alignment.Left)
                    baselinePos.X = 0;
                if (XAlignment == Alignment.Center)
                    baselinePos.X = ((float)args.width - GetTextSize(str).X) / 2;
                if (XAlignment == Alignment.Right)
                    baselinePos.X = (float)args.width - GetTextSize(str).X;
            }
            uint i = 0;
            ResetOffset(s[i]);
            foreach (char c in _text)
            {
                if (c == '\n')
                {
                    i++;
                    ResetOffset(s[i]);
                    baselinePos.Y += lineHeight * FontScale;
                    continue;
                }

                if (c == ' ')
                {
                    float spaceWidth = font.Size * font.SpaceWidth;
                    baselinePos.X += spaceWidth * FontScale * LetterSpacing;
                    continue;
                }

                if (NullChar.Contains(c))
                    continue;

                if (!_charCache.TryGetValue(c, out var cache))
                {
                    cache = font.GetFontTexture(c);
                    _charCache[c] = cache;
                }

                // 计算字形矩形的左上角（屏幕坐标）
                float left = baselinePos.X + cache.BearingX;
                float top;
                top = baselinePos.Y + cache.BearingY * FontScale;
                Vector2 position = new Vector2(left, top);
                Vector2 size = new Vector2(cache.Width, cache.Height) * FontScale;

                if (cache.Texture != null && cache.ResourceSet != null)
                {
                    DrawCharacter(position, size, cache, collector);
                }

                // 前进到下一个字符
                baselinePos.X += cache.Advance * FontScale * LetterSpacing;
            }
        };
    }

    private void DrawCharacter(
        Vector2 position,
        Vector2 size,
        FontTexture cache,
        UIDrawCollector collector
    )
    {
        // 构建顶点并提交给 collector
        // 参考之前的 renderAText 逻辑，但直接使用屏幕坐标，不再乘缩放
        var tl = new WindowsRenderer.Vertex(
            position,
            color,
            new(new(), new(0, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var tr = new WindowsRenderer.Vertex(
            position + new Vector2(size.X, 0),
            color,
            new(new(), new(1, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var bl = new WindowsRenderer.Vertex(
            position + new Vector2(0, size.Y),
            color,
            new(new(), new(0, 1)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var br = new WindowsRenderer.Vertex(
            position + size,
            color,
            new(new(), new(1, 1)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        collector.DrawVertex([tl, tr, bl], this);
        collector.DrawVertex([tr, bl, br], this);
    }

    readonly ResourceManager rm;

    public Vector2 GetTextSize(string s)
    {
        var font = rm.GetResource(FontId) as Font;
        if (font == null)
            return Vector2.One;
        if (string.IsNullOrEmpty(s))
            return Vector2.Zero;

        float lineHeight = font.Size / 1.4f * FontScale;
        float maxWidth = 0;
        float currentWidth = 0;
        int lineCount = s.Split('\n').Length;

        foreach (char c in s)
        {
            if (c == '\n')
            {
                maxWidth = Math.Max(maxWidth, currentWidth);
                currentWidth = 0;
                continue;
            }

            // 字符前进量（与渲染完全一致）
            float advance;
            if (c == ' ')
            {
                advance = font.Size * font.SpaceWidth * FontScale * LetterSpacing;
            }
            else if (NullChar.Contains(c))
            {
                continue;
            }
            else
            {
                // 确保字符已缓存（否则临时获取度量）
                if (!_charCache.TryGetValue(c, out var cache))
                {
                    cache = font.GetFontTexture(c);
                    _charCache[c] = cache;
                }
                advance = cache.Advance * FontScale * LetterSpacing;
            }
            currentWidth += advance;
        }
        maxWidth = Math.Max(maxWidth, currentWidth);

        return new Vector2(maxWidth, lineHeight * lineCount);
    }

    public float LetterSpacing { get; set; } = 1f;
    public Alignment XAlignment { get; set; } = Alignment.Left;
    public Alignment YAlignment { get; set; } = Alignment.Left;
}

public enum Alignment
{
    Left,
    Center,
    Right,
}
