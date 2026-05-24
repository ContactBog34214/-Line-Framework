using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using FreeTypeSharp;
using TagLib.Riff;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI;

public class UIDrawCollector
{
    public class DrawCommand
    {
        public float Z;
        public Rectangle Rect;
        public float Rotation;
        public Vector2 Anchor;
        public UIWidget Source;
    }

    public class DrawRectCommand : DrawCommand
    {
        public RgbaFloat Color;
        public float Opacity;
    }

    public class DrawTextureCommand : DrawCommand
    {
        public Texture Texture;
        public RgbaFloat Tint;
        public ResourceSet TextureResourceSet;
    }

    public class DrawTextCommand : DrawCommand
    {
        public string Text;
        public RgbaFloat Color;
        public float FontSize;
    }

    public List<DrawRectCommand> Rects = [];
    public List<DrawTextureCommand> Textures = [];
    public List<DrawTextCommand> Texts = [];
    public List<DrawCommand> AllCommands = new List<DrawCommand>();

    public void Update()
    {
        AllCommands.Clear();
        AllCommands.AddRange(Rects);
        AllCommands.AddRange(Textures);
        AllCommands.AddRange(Texts);
        AllCommands.Sort((a, b) => a.Z.CompareTo(b.Z));
    }

    public void Clear()
    {
        Rects.Clear();
        Textures.Clear();
        Texts.Clear();
        AllCommands.Clear();
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
                Z = source.oz,
            }
        );

    public void DrawTexture(
        Rectangle rect,
        float rotation,
        Vector2 anchor,
        ResourceSet textureResourceSet,
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
                TextureResourceSet = textureResourceSet,
                Z = source.oz,
            }
        );

    public void DrawText(
        Rectangle rect,
        float rotation,
        Vector2 anchor,
        string text,
        RgbaFloat color,
        UIWidget source,
        float fontsize
    ) =>
        Texts.Add(
            new DrawTextCommand
            {
                Rect = rect,
                Text = text,
                Color = color,
                Rotation = rotation,
                Anchor = anchor,
                Source = source,
                FontSize = fontsize,
                Z = source.oz,
            }
        );
}
