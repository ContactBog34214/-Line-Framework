using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using TagLib.Riff;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI;

public class UIDrawCollector
{
    public struct DrawRectCommand
    {
        public Rectangle Rect;
        public RgbaFloat Color;
        public float Rotation;
        public Vector2 Anchor;
        public float Opacity;
        public UIWidget Source;
    }

    public struct DrawTextureCommand
    {
        public Rectangle Rect;
        public Texture Texture;
        public RgbaFloat Tint;
        public float Rotation;
        public Vector2 Anchor;
        public float Opacity;
        public UIWidget Source;
    }

    public struct DrawTextCommand
    {
        public Rectangle Rect;
        public string Text;
        public Font Font;
        public RgbaFloat Color;
        public float Rotation;
        public Vector2 Anchor;
        public UIWidget Source;
    }

    public List<DrawRectCommand> Rects = [];
    public List<DrawTextureCommand> Textures = [];
    public List<DrawTextCommand> Texts = [];

    public void Clear()
    {
        Rects.Clear();
        Textures.Clear();
        Texts.Clear();
    }

    public void DrawRect(
        Rectangle rect,
        float rotation,
        Vector2 anchor,
        RgbaFloat color,
        UIWidget source
    ) =>
        Rects.Add(
            new DrawRectCommand
            {
                Rect = rect,
                Color = color,
                Rotation = rotation,
                Anchor = anchor,
                Source = source,
            }
        );

    public void DrawTexture(
        Rectangle rect,
        float rotation,
        Vector2 anchor,
        Texture texture,
        RgbaFloat tint,
        UIWidget source
    ) =>
        Textures.Add(
            new DrawTextureCommand
            {
                Rect = rect,
                Texture = texture,
                Tint = tint,
                Rotation = rotation,
                Anchor = anchor,
                Source = source,
            }
        );

    public void DrawText(
        Rectangle rect,
        float rotation,
        Vector2 anchor,
        string text,
        Font font,
        RgbaFloat color,
        UIWidget source
    ) =>
        Texts.Add(
            new DrawTextCommand
            {
                Rect = rect,
                Text = text,
                Font = font,
                Color = color,
                Rotation = rotation,
                Anchor = anchor,
                Source = source,
            }
        );
}
