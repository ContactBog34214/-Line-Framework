using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Graphic;
using Veldrid;

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
    public float FontScale
    {
        get
        {
            var f = rm.GetResource(FontId) as Font;
            if (f == null)
                return 0;
            //            字体大小 = 缩放 * 实际大小
            // 字体大小 / 实际大小 = 缩放
            return FontSize / f.Size;
        }
    }
    public float FontSize
    {
        get;
        set
        {
            if (0 < value)
                field = value;
            else
                throw new InvalidDataException($"FontSize cannot be {value}");
        }
    } = 48;

    private readonly Dictionary<char, FontTexture> _charCache = new();

    public override void RendererContext(RendererContextArgs args)
    {
        if (RenderAction == null)
            return;
        RenderAction(args);
    }

    public Vector2 GetWhereIndexCharIs(string Text, int Index)
    {
        if (Index >= Text.Length)
            Index = Text.Length - 1;
        if (Index <= 0)
            return new(0);
        string[] AllLines = Text.Split('\n');
        var ot = 1;
        if (Text.Substring(0, Index - 1).Split('\n').Length == AllLines.Length)
        {
            Index += 1;
        }

        var sub = Text.Substring(0, Index);
        string[] AllLinesBeforeCur = sub.Split('\n');

        var s = GetTextSize(AllLinesBeforeCur.Last());
        var Height = GetTextSize(" ").Y;

        if (AllLinesBeforeCur.Length == AllLines.Length - 1 && Text.ToArray().Last() == '\n')
            ot--;

        return new(s.X, Height * (AllLinesBeforeCur.Length - ot));
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
            float lineHeight = FontSize / 1.4f;
            Vector2 baselinePos = new Vector2(0, 0);
            var s = _text.Split('\n');

            baselinePos.Y = -font.Ascender * FontScale;
            if (YAlignment == Alignment.Center)
                baselinePos.Y += ((float)args.height - s.Length * lineHeight) / 2f;
            if (YAlignment == Alignment.Right)
                baselinePos.Y += (float)args.height - s.Length * lineHeight;
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
                    baselinePos.Y += lineHeight;
                    continue;
                }

                if (c == ' ')
                {
                    float spaceWidth = FontSize * font.SpaceWidth;
                    baselinePos.X += spaceWidth * LetterSpacing;
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
            position + Offset,
            color,
            new(new(), new(0, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var tr = new WindowsRenderer.Vertex(
            position + new Vector2(size.X, 0) + Offset,
            color,
            new(new(), new(1, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var bl = new WindowsRenderer.Vertex(
            position + new Vector2(0, size.Y) + Offset,
            color,
            new(new(), new(0, 1)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var br = new WindowsRenderer.Vertex(
            position + size + Offset,
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

        float lineHeight = FontSize / 1.4f;
        if (string.IsNullOrEmpty(s))
            return new(0, lineHeight);
        float maxWidth = 0;
        float currentWidth = 0;
        int lineCount = 1;

        foreach (char c in s)
        {
            if (c == '\n')
            {
                maxWidth = Math.Max(maxWidth, currentWidth);
                currentWidth = 0;
                lineCount++;
                continue;
            }

            // 字符前进量（与渲染完全一致）
            float advance;
            if (c == ' ')
            {
                advance = font.SpaceWidth * FontSize * LetterSpacing;
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
    public Vector2 Offset { get; set; } = new(0);
    public Alignment XAlignment { get; set; } = Alignment.Left;
    public Alignment YAlignment { get; set; } = Alignment.Left;
}

public enum Alignment
{
    Left,
    Center,
    Right,
}
