using System.Collections.Concurrent;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.Resource;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using Line.Framework.UI;
using Veldrid;
using RgbaFloat = Line.Framework.Types.RgbaFloat;

namespace Line.Framework.Default.UIWidgets;

public sealed class UIText : UIWidget
{
    public List<Texture> FontTexture { get; private set; } = [];
    public TrackableList<string> FontId { get; set; } = new();
    public DynamicValue<RgbaFloat> color { get; set; } = new RgbaFloat(1, 1, 1, 1);
    private readonly ConcurrentDictionary<char, Font> _charCache = new();
    public DynamicValue<string> Text
    {
        get => _text;
        set => SetText(value);
    }
    string _text = "";
    public DynamicValue<float> FontSize { get; set; } = 48;

    public override async Task RendererContext(RendererContextArgs args)
    {
        if ((FontId?.Count ?? 0) <= 0)
            return;
        if (FontId?.IsDirty ?? false)
        {
            _charCache.Clear();
            FontId.ResetDirty();
        }
        var collector = args.Collector;
        if (rm == null)
            return;
        Font font = null;
        double FontScale = 0;
        UseFontIndex(0, out font, out FontScale);
        if (font == null)
            return;

        // ---------- 坐标系配置 ----------
        // 如果屏幕 Y 轴向下为正（左上角原点），设为 true；Y 轴向上为正（左下角原点），设为 false

        // 基线起始位置（屏幕坐标）
        float lineHeight = FontSize / 1.4f;
        Vector2 baselinePos = new Vector2(0, 0);
        var s = _text.Split('\n');

        baselinePos.Y = (float)(-font.Ascender * FontScale);
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
        bool SkipLine = false;
        foreach (char c in _text)
        {
            SelectFont(c, out font, out FontScale);
            if (c == '\n')
            {
                i++;
                ResetOffset(s[i]);
                baselinePos.Y += lineHeight;
                SkipLine = false;
                continue;
            }
            else if (SkipLine || baselinePos.Y + Offset.Y < 0)
                continue;

            if (c == ' ')
            {
                float spaceWidth = FontSize * font.SpaceWidth;
                baselinePos.X += spaceWidth * LetterSpacing;
                continue;
            }

            if (font == null)
                continue;
            FontTexture cache = null;
            if (font.HasCache(c))
                cache = await font.GetFontTexture(c);
            else
            {
                font.CreateCharTexture(c);
                baselinePos.X += FontSize * LetterSpacing;
                continue;
            }

            // 计算字形矩形的左上角（屏幕坐标）
            float left = (float)(baselinePos.X + cache.BearingX * FontScale);
            double top;
            top = baselinePos.Y + cache.BearingY * FontScale;
            Vector2 position = new Vector2(left, (float)top) + Offset;
            Vector2 size = new Vector2(cache.Width, cache.Height) * (float)FontScale;

            if (cache?.Texture != null && cache?.ResourceSet != null)
            {
                bool r = true;
                if (position.X > args.width)
                {
                    r = false;
                    SkipLine = true;
                }
                if (baselinePos.Y + Offset.Y - lineHeight > args.height)
                {
                    r = false;
                    break;
                }
                if (position.X + size.X < 0)
                    r = false;
                if (position.Y + size.Y < 0)
                    r = false;
                if (r)
                    DrawCharacter(position, size, cache, collector);
            }

            // 前进到下一个字符
            baselinePos.X += (float)((cache?.Advance ?? FontSize) * FontScale * LetterSpacing);
        }
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

    void SetText(DynamicValue<string> s)
    {
        if (s == _text)
            return;
        _text = s;
    }

    private void UseFontIndex(int Index, out Font font, out double FontScale)
    {
        font = null;
        FontScale = 0;
        if (Index < 0 || FontId.Count <= Index)
            return;
        Font tmp = null;
        if (rm.ResourceIsLoaded(FontId[Index]))
            tmp = rm.GetResource<Font>(FontId[Index]).GetAwaiter().GetResult();
        if (tmp == null)
        {
            UseFontIndex(Index + 1, out font, out FontScale);
            if (!rm.ResourceIsLoaded(FontId[Index]))
                rm.LoadResource(FontId[Index]).GetAwaiter().GetResult();
            return;
        }
        font = tmp;
        FontScale = FontSize / (double)font.Size;
    }

    private void SelectFont(char c, out Font font, out double FontScale)
    {
        if (_charCache.TryGetValue(c, out font))
        {
            FontScale = FontSize / (double)font.Size;
            return;
        }
        font = null;
        FontScale = 0;
        if ((FontId?.Count ?? 0) == 0)
            return;
        foreach (var i in FontId)
        {
            font = rm.GetResource<Font>(i).GetAwaiter().GetResult();
            if (font == null)
                continue;
            FontScale = FontSize / (double)font.Size;
            FontTexture g = null;
            if (font.HasCache(c))
                g = font.GetFontTexture(c).GetAwaiter().GetResult();
            else
                font.CreateCharTexture(c);

            if ((g?.Width ?? 0) * (g?.Height ?? 0) > 0 || c == ' ')
            {
                _charCache.TryAdd(c, font);
                return;
            }
        }
    }

    public UIText(ResourceManager manager)
    {
        rm = manager;
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
        var tl = new Vertex(
            position,
            color.Value,
            new(new(), new(0, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var tr = new Vertex(
            position + new Vector2(size.X, 0),
            color,
            new(new(), new(1, 0)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var bl = new Vertex(
            position + new Vector2(0, size.Y),
            color,
            new(new(), new(0, 1)),
            cache.Texture,
            cache.ResourceSet,
            1
        );
        var br = new Vertex(
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
        if ((FontId?.Count ?? 0) == 0)
            return Vector2.Zero;
        UseFontIndex(0, out var font, out var FontScale);
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
            SelectFont(c, out font, out FontScale);
            if (c == ' ')
            {
                advance = font.SpaceWidth * FontSize * LetterSpacing;
            }
            else
            {
                if (font == null)
                    continue;
                FontTexture cache = null;
                cache = font.GetFontTexture(c).GetAwaiter().GetResult();
                advance = (float)(cache.Advance * FontScale * LetterSpacing);
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
