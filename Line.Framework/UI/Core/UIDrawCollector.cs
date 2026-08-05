using System.Numerics;
using Line.Framework.Graphics;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI;

public class UIDrawCollector
{
    public class DrawCommand
    {
        public float Z;

        public float Rotation;
        public Vector2 Anchor;
        public UIWidget Source;
    }

    public class DrawRectCommand : DrawCommand
    {
        public Rectangle Rect;
        public RgbaFloat Color;
        public float Opacity;
    }

    public class DrawTextureCommand : DrawCommand
    {
        public Rectangle Rect;
        public Texture Texture;
        public RgbaFloat Tint;
        public ResourceSet TextureResourceSet;
    }

    public class DrawVertCommand : DrawCommand
    {
        public WindowsRenderer.Vertex[] Vert;
    }

    public List<DrawRectCommand> Rects = [];
    public List<DrawTextureCommand> Textures = [];
    public List<DrawVertCommand> Verts = [];

    public List<DrawCommand> AllCommands = new List<DrawCommand>();

    public void Update()
    {
        AllCommands.Clear();
        AllCommands.AddRange(Rects);
        AllCommands.AddRange(Textures);
        AllCommands.AddRange(Verts);
        AllCommands.OrderBy(a => a.Source.oz);
    }

    public void Clear()
    {
        Rects.Clear();
        Textures.Clear();
        Verts.Clear();
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

    private Object vertLock = new();

    public void DrawVertex(WindowsRenderer.Vertex[] v, UIWidget source)
    {
        if (v.Length % 3 != 0)
        {
            var t = v.ToList();
            bool two = v.Length % 3 == 2;
            t.RemoveAt(t.Count - 1);
            if (two)
                t.RemoveAt(t.Count - 1);
            v = t.ToArray();
        }
        lock (vertLock)
        {
            Verts.Add(
                new()
                {
                    Vert = v,
                    Z = source.oz,
                    Source = source,
                }
            );
        }
    }
}
