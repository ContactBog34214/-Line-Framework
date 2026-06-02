using System.Numerics;
using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.UI.DefaultWidget;

public class UIText : UIWidget
{
    public List<Texture> FontTexture { get; private set; } = [];
    private FontManager manager;
    private GraphicsDevice graphic;
    private ResourceLayout resl;
    private List<ResourceSet> rs = [];
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
        RenderText();
    }

    public void RenderText()
    {
        try
        {
            if (manager == null)
                return;
            manager.SetFontSize(_size);

            //清除之前的
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
            void RenderAText(char c)
            {
                if (c == ' ')
                {
                    FontTexture.Add(null);
                    rs.Add(null);
                    return;
                }
                var (grayPixels, width, height) = manager.GetTextPixels(c.ToString());
                FontTexture.Add(CreateColoredTexture(grayPixels, width, height, color));
                rs.Add(
                    graphic.ResourceFactory.CreateResourceSet(
                        new ResourceSetDescription(resl, FontTexture[FontTexture.Count - 1])
                    )
                );
            }
            foreach (var i in _text.ToCharArray())
            {
                RenderAText(i);
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
            var s = GetSizeOnScreen();
            if (s.X <= 0 && s.Y <= 0)
            {
                return;
            }
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
            float offset = 0;
            for (int i = 0; i < FontTexture.Count; i++)
            {
                Vector2 thisSize = GetTextSize(_text[i]);
                Vector2 RenderSize = new(
                    thisSize.X / totSize.X * (float)args.width,
                    thisSize.Y / totSize.Y * (float)args.height
                );
                renderAText(
                    new(offset, ((float)args.height - RenderSize.Y) / 2),
                    RenderSize,
                    FontTexture[i],
                    rs[i]
                );
                offset += thisSize.X;
            }
        };
    }

    private Vector2 totSize = new();

    private Texture CreateColoredTexture(
        byte[] grayPixels,
        uint width,
        uint height,
        RgbaFloat textColor
    )
    {
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

    public Vector2 GetTextSize(string s)
    {
        float Width = 0;
        float MaximumHeight = 0;
        foreach (var i in s.ToCharArray())
        {
            var tmp = GetTextSize(i);
            Width += tmp.X;
            if (tmp.Y > MaximumHeight)
            {
                MaximumHeight = tmp.Y;
            }
        }
        return new(Width, MaximumHeight);
    }

    public Vector2 GetTextSize(char s)
    {
        if (' ' == s)
        {
            return (new(FontSize * SpaceWidth, 1));
        }
        var a = manager.GetTextSize(s.ToString(), FontSize);
        return new(a.width, a.height);
    }

    public float SpaceWidth = 0.4f;
}
