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
    public DynamicValue<bool> AutoBreakLine { get; set; } = false;

    public List<Texture> FontTexture { get; private set; } = [];

    public TrackableList<string> FontId { get; set; } = new();

    public DynamicValue<RgbaFloat> color { get; set; } =
        new RgbaFloat(1, 1, 1, 1);

    private readonly ConcurrentDictionary<char, Font> _charCache = new();

    public DynamicValue<string> Text
    {
        get => _text;
        set => SetText(value);
    }

    private string _text = "";

    /// <summary>
    /// 实际文字字号，支持浮点值。
    /// </summary>
    public DynamicValue<float> FontSize { get; set; } = 48;

    /// <summary>
    /// 相邻文本行 baseline 间距相对于字体默认行高的缩放。
    ///
    /// 1.0 = 默认行距
    /// 0.7 = 默认行距的 70%
    /// 1.5 = 默认行距的 150%
    /// </summary>
    public float LineSpacing { get; set; } = 0.7f;

    /// <summary>
    /// 字符前进量缩放。
    /// 1.0 = 默认
    /// </summary>
    public float LetterSpacing { get; set; } = 1f;

    public Vector2 Offset { get; set; } = Vector2.Zero;

    public Alignment XAlignment { get; set; } = Alignment.Left;

    public Alignment YAlignment { get; set; } = Alignment.Left;

    readonly ResourceManager rm;


    public UIText(ResourceManager manager)
    {
        rm = manager;
    }


    private float GetRequestedFontSize()
    {
        return MathF.Max(
            FontSize?.Value ?? 24f,
            1f);
    }


    #region Font Selection

    private bool TryGetFirstLoadedFont(
        out Font font)
    {
        font = null;

        if (FontId == null ||
            FontId.Count == 0)
        {
            return false;
        }

        foreach (var id in FontId)
        {
            if (!rm.ResourceIsLoaded(id))
                continue;

            var candidate =
                rm.GetResource<Font>(id)
                    .GetAwaiter()
                    .GetResult();

            if (candidate == null)
                continue;

            font = candidate;
            return true;
        }

        return false;
    }


    private bool TrySelectLoadedFont(
        char c,
        out Font font)
    {
        if (_charCache.TryGetValue(
                c,
                out font))
        {
            return true;
        }

        font = null;

        if (FontId == null ||
            FontId.Count == 0)
        {
            return false;
        }

        foreach (var id in FontId)
        {
            if (!rm.ResourceIsLoaded(id))
                continue;

            var candidate =
                rm.GetResource<Font>(id)
                    .GetAwaiter()
                    .GetResult();

            if (candidate == null)
                continue;

            if (!candidate.HasGlyph(c))
                continue;

            font = candidate;

            _charCache.TryAdd(
                c,
                candidate);

            return true;
        }

        return false;
    }


    private bool TrySelectFont(
        char c,
        out Font font)
    {
        if (_charCache.TryGetValue(
                c,
                out font))
        {
            return true;
        }

        font = null;

        if (FontId == null ||
            FontId.Count == 0)
        {
            return false;
        }

        foreach (var id in FontId)
        {
            /*
             * Renderer 可以负责加载字体。
             */
            var candidate =
                rm.GetResource<Font>(id)
                    .GetAwaiter()
                    .GetResult();

            if (candidate == null)
                continue;

            if (!candidate.HasGlyph(c))
                continue;

            font = candidate;

            _charCache.TryAdd(
                c,
                candidate);

            return true;
        }

        return false;
    }

    #endregion


    #region Auto Break

    /// <summary>
    /// 根据当前 UIText 的宽度进行自动换行。
    ///
    /// 这里绝对不创建 GlyphTexture。
    /// 只使用字体 metrics。
    /// </summary>
    public string GetTextAftarAutoBreakLine(
        string source)
    {
        if (!AutoBreakLine)
            return source;

        if (string.IsNullOrEmpty(source))
            return source;

        float fontSize =
            GetRequestedFontSize();

        float availableWidth =
            GetSizeOnScreen().X;

        if (availableWidth <= 0)
            return source;

        var result = new System.Text.StringBuilder();

        float currentWidth = 0;

        foreach (char c in source)
        {
            if (c == '\n')
            {
                result.Append('\n');
                currentWidth = 0;
                continue;
            }

            if (!TrySelectLoadedFont(
                    c,
                    out var font))
            {
                /*
                 * 字体尚未加载时不要猜。
                 * 保留字符，不进行额外换行。
                 */
                result.Append(c);
                continue;
            }

            float advance;

            if (c == ' ')
            {
                advance =
                    font.SpaceWidth *
                    fontSize;
            }
            else
            {
                advance =
                    font.GetGlyphAdvance(
                        c,
                        fontSize);
            }

            advance *= LetterSpacing;

            if (currentWidth > 0 &&
                currentWidth + advance >
                availableWidth)
            {
                result.Append('\n');
                currentWidth = 0;
            }

            result.Append(c);
            currentWidth += advance;
        }

        return result.ToString();
    }

    #endregion


    #region Layout

    private float GetLineHeight(
        Font font,
        float fontSize)
    {
        return font.GetLineHeight(fontSize)
            * LineSpacing;
    }


    private float GetTextBlockHeight(
        Font font,
        float fontSize,
        int lineCount)
    {
        float ascender =
            font.GetAscender(fontSize);

        float descender =
            font.GetDescender(fontSize);

        float baseLineHeight =
            font.GetLineHeight(fontSize);

        float lineStep =
            baseLineHeight * LineSpacing;

        if (lineCount <= 1)
        {
            return ascender - descender;
        }

        return ascender
            - descender
            + (lineCount - 1) * lineStep;
    }

    #endregion


    #region Renderer

    public override async Task RendererContext(
        RendererContextArgs args)
    {
        var collector = args.Collector;

        if (FontId == null ||
            FontId.Count == 0)
        {
            return;
        }

        if (FontId.IsDirty)
        {
            _charCache.Clear();
            FontId.ResetDirty();
        }

        if (rm == null)
            return;

        float fontSize =
            GetRequestedFontSize();

        /*
         * 第一套字体用于确定整个文本的 line box。
         */
        if (!TryGetFirstLoadedFont(
                out var firstFont))
        {
            return;
        }

        /*
         * 自动换行后的文本。
         */
        string text =
            GetTextAftarAutoBreakLine(_text);

        string[] lines =
            text.Split('\n');

        if (lines.Length == 0)
            lines = [""];

        /*
         * 字体自身 metrics。
         */
        float ascender =
            firstFont.GetAscender(fontSize);

        float descender =
            firstFont.GetDescender(fontSize);

        float baseLineHeight =
            firstFont.GetLineHeight(fontSize);

        /*
         * 真正的 baseline step。
         *
         * 注意：
         * LineSpacing 只影响行与行之间的距离，
         * 不影响 ascender / descender。
         */
        float lineStep =
            baseLineHeight * LineSpacing;

        /*
         * 计算整个文本块高度。
         */
        float textBlockHeight =
            ascender
            - descender
            + (lines.Length - 1)
            * lineStep;

        /*
         * 根据 YAlignment 计算第一条 baseline。
         */
        float baselineY;

        if (YAlignment == Alignment.Center)
        {
            baselineY =
                ((float)args.height
                 - textBlockHeight)
                / 2f
                + ascender;
        }
        else if (YAlignment == Alignment.Right)
        {
            baselineY =
                (float)args.height
                + descender
                - (lines.Length - 1)
                * lineStep;
        }
        else
        {
            baselineY =
                ascender;
        }

        Vector2 baselinePos =
            new(0, baselineY);

        uint lineIndex = 0;

        ResetLineOffset(
            lines[lineIndex],
            ref baselinePos,
            args);

        bool skipLine = false;

        foreach (char c in text)
        {
            /*
             * 换行。
             */
            if (c == '\n')
            {
                lineIndex++;

                if (lineIndex >= lines.Length)
                    break;

                baselinePos.Y +=
                    lineStep;

                ResetLineOffset(
                    lines[lineIndex],
                    ref baselinePos,
                    args);

                skipLine = false;

                continue;
            }

            /*
             * 当前行已经超出右侧。
             */
            if (skipLine)
                continue;

            /*
             * 整行已经完全在控件顶部之外。
             */
            if (baselinePos.Y +
                Offset.Y +
                descender < 0)
            {
                continue;
            }

            /*
             * 空格不需要 Texture。
             */
            if (c == ' ')
            {
                Font fontForSpace;

                if (!TrySelectFont(
                        c,
                        out fontForSpace))
                {
                    /*
                     * 没有定义字体时，
                     * 使用当前字号作为最基本 fallback。
                     */
                    baselinePos.X +=
                        fontSize *
                        0.25f *
                        LetterSpacing;

                    continue;
                }

                baselinePos.X +=
                    fontSize *
                    fontForSpace.SpaceWidth *
                    LetterSpacing;

                continue;
            }

            if (!TrySelectFont(
                    c,
                    out var font))
            {
                continue;
            }

            FontTexture cache;

            /*
             * 当前 glyph 已经生成。
             */
            if (font.HasCache(
                    c,
                    fontSize))
            {
                cache =
                    await font.GetFontTexture(
                        c,
                        fontSize);
            }
            else
            {
                /*
                 * 请求后台生成。
                 */
                font.CreateCharTexture(
                    c,
                    fontSize);

                /*
                 * 当前帧先使用 metrics advance
                 * 推进光标，而不是随便加 FontSize。
                 */
                baselinePos.X +=
                    font.GetGlyphAdvance(
                        c,
                        fontSize)
                    * LetterSpacing;

                continue;
            }

            if (cache == null)
                continue;

            /*
             * 不使用 cache.Scale。
             *
             * cache 的 bitmap 是按照
             * ceil(fontSize) rasterized 的。
             *
             * 这里直接算目标尺寸 / bitmap 尺寸。
             *
             * 同一个 Font 可以同时被多个
             * UIText 使用不同浮点字号，
             * 不会互相污染。
             */
            float scale =
                fontSize /
                MathF.Max(
                    cache.FontSize,
                    1f);

            /*
             * Glyph 相对于 baseline 的位置。
             */
            float left =
                baselinePos.X
                + cache.BearingX
                * scale;

            float top =
                baselinePos.Y
                + cache.BearingY
                * scale;

            Vector2 position =
                new Vector2(left, top)
                + Offset;

            Vector2 glyphSize =
                new(
                    cache.Width * scale,
                    cache.Height * scale);

            /*
             * 视口裁剪。
             */
            bool draw = true;

            if (position.X >
                args.width)
            {
                draw = false;
                skipLine = true;
            }

            if (position.X +
                glyphSize.X < 0)
            {
                draw = false;
            }

            if (position.Y +
                glyphSize.Y < 0)
            {
                draw = false;
            }

            if (position.Y >
                args.height)
            {
                draw = false;
            }

            if (draw &&
                cache.Texture != null &&
                cache.ResourceSet != null)
            {
                DrawCharacter(
                    position,
                    glyphSize,
                    cache,
                    collector);
            }

            /*
             * Advance 必须与 bitmap 使用同样的 scale。
             */
            baselinePos.X +=
                cache.Advance
                * scale
                * LetterSpacing;
        }
    }


    private void ResetLineOffset(
        string line,
        ref Vector2 baselinePos,
        RendererContextArgs args)
    {
        if (XAlignment == Alignment.Left)
        {
            baselinePos.X = 0;
            return;
        }

        float width =
            GetLineTextWidth(line);

        if (XAlignment == Alignment.Center)
        {
            baselinePos.X =
                ((float)args.width - width)
                / 2f;
        }
        else if (XAlignment == Alignment.Right)
        {
            baselinePos.X =
                (float)args.width - width;
        }
    }


    private float GetLineTextWidth(
        string line)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        float fontSize =
            GetRequestedFontSize();

        float width = 0;

        foreach (char c in line)
        {
            if (!TrySelectLoadedFont(
                    c,
                    out var font))
            {
                continue;
            }

            if (c == ' ')
            {
                width +=
                    font.SpaceWidth
                    * fontSize
                    * LetterSpacing;
            }
            else
            {
                width +=
                    font.GetGlyphAdvance(
                        c,
                        fontSize)
                    * LetterSpacing;
            }
        }

        return width;
    }

    #endregion


    #region Size

    public Vector2 GetTextSize(
        string s)
    {
        if (FontId == null ||
            FontId.Count == 0)
        {
            return Vector2.Zero;
        }

        float fontSize =
            GetRequestedFontSize();

        if (!TryGetFirstLoadedFont(
                out var firstFont))
        {
            return Vector2.Zero;
        }

        if (string.IsNullOrEmpty(s))
        {
            float emptyHeight =
                firstFont.GetAscender(fontSize)
                - firstFont.GetDescender(fontSize);

            return new(
                0,
                emptyHeight);
        }

        float maxWidth = 0;
        float currentWidth = 0;

        int lineCount = 1;

        /*
         * 注意：
         *
         * GetTextSize 本身只负责测量原始文本，
         * 不读取自身 Size。
         *
         * 因此不会出现：
         *
         * Size
         *  ↓
         * GetTextSize
         *  ↓
         * Size
         *
         * 的循环依赖。
         */
        foreach (char c in s)
        {
            if (c == '\n')
            {
                maxWidth =
                    MathF.Max(
                        maxWidth,
                        currentWidth);

                currentWidth = 0;
                lineCount++;

                continue;
            }

            if (!TrySelectLoadedFont(
                    c,
                    out var font))
            {
                continue;
            }

            float advance;

            if (c == ' ')
            {
                advance =
                    font.SpaceWidth
                    * fontSize;
            }
            else
            {
                advance =
                    font.GetGlyphAdvance(
                        c,
                        fontSize);
            }

            currentWidth +=
                advance * LetterSpacing;
        }

        maxWidth =
            MathF.Max(
                maxWidth,
                currentWidth);

        float height =
            GetTextBlockHeight(
                firstFont,
                fontSize,
                lineCount);

        return new Vector2(
            maxWidth,
            height);
    }

    #endregion


    #region Character Position

    public Vector2 GetWhereIndexCharIs(
        string text,
        int index)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        if (index < 0)
            index = 0;

        if (index > text.Length)
            index = text.Length;

        string before =
            text[..index];

        string[] lines =
            before.Split('\n');

        string currentLine =
            lines.Length > 0
                ? lines[^1]
                : "";

        Vector2 lineSize =
            GetTextSize(currentLine);

        float fontSize =
            GetRequestedFontSize();

        if (!TryGetFirstLoadedFont(
                out var font))
        {
            return new(
                lineSize.X,
                0);
        }

        float lineStep =
            font.GetLineHeight(fontSize)
            * LineSpacing;

        float y =
            MathF.Max(
                lines.Length - 1,
                0)
            * lineStep;

        return new(
            lineSize.X,
            y);
    }

    #endregion


    #region Text

    private void SetText(
        DynamicValue<string> value)
    {
        if (value == _text)
            return;

        _text = value;
    }

    #endregion


    #region Drawing

    private void DrawCharacter(
        Vector2 position,
        Vector2 size,
        FontTexture cache,
        UIDrawCollector collector)
    {
        var tl =
            new Vertex(
                position,
                color.Value,
                new(new(), new(0, 0)),
                cache.Texture,
                cache.ResourceSet,
                1);

        var tr =
            new Vertex(
                position +
                    new Vector2(size.X, 0),
                color.Value,
                new(new(), new(1, 0)),
                cache.Texture,
                cache.ResourceSet,
                1);

        var bl =
            new Vertex(
                position +
                    new Vector2(0, size.Y),
                color.Value,
                new(new(), new(0, 1)),
                cache.Texture,
                cache.ResourceSet,
                1);

        var br =
            new Vertex(
                position + size,
                color.Value,
                new(new(), new(1, 1)),
                cache.Texture,
                cache.ResourceSet,
                1);

        collector.DrawVertex(
            [tl, tr, bl],
            this);

        collector.DrawVertex(
            [tr, bl, br],
            this);
    }

    #endregion
}


public enum Alignment
{
    Left,
    Center,
    Right,
}