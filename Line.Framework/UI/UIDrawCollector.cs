using System.Numerics;
using Line.Framework.Graphics;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI;

public class UIDrawCollector
{

    public class DrawCommand
    {
        public Vertex[] Vert;
        public float Z;

        public float Rotation;
        public Vector2 Anchor;
        public UIWidget Source;
    }

    public List<DrawCommand> Verts = [];

    public List<DrawCommand> AllCommands = new List<DrawCommand>();

        public void Update()
        {
            AllCommands.Clear();

            AllCommands.AddRange(Verts);
        }

    public void Clear()
    {
        Verts.Clear();
        AllCommands.Clear();
    }

    public void DrawRect(Rectangle rect, RgbaFloat color, UIWidget source)
    {
        var tl = new Vertex(
            new Vector2(0, 0) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(0, 0)),
            null,
            null,
            1
        );
        var tr = new Vertex(
            new Vector2(rect.Width, 0) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(1, 0)),
            null,
            null,
            1
        );
        var bl = new Vertex(
            new Vector2(0, rect.Height) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(0, 1)),
            null,
            null,
            1
        );
        var br = new Vertex(
            new Vector2(rect.Width, rect.Height) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(1, 1)),
            null,
            null,
            1
        );
        DrawVertex([tl, tr, bl], source);
        DrawVertex([tr, bl, br], source);
    }

    public void DrawTexture(
        Rectangle rect,
        ResourceSet textureResourceSet,
        Texture texture,
        RgbaFloat color,
        UIWidget source
    )
    {
        var tl = new Vertex(
            new Vector2(0, 0) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(0, 0)),
            texture,
            textureResourceSet,
            1
        );
        var tr = new Vertex(
            new Vector2(rect.Width, 0) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(1, 0)),
            texture,
            textureResourceSet,
            1
        );
        var bl = new Vertex(
            new Vector2(0, rect.Height) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(0, 1)),
            texture,
            textureResourceSet,
            1
        );
        var br = new Vertex(
            new Vector2(rect.Width, rect.Height) + new Vector2(rect.X, rect.Y),
            color,
            new(new(), new(1, 1)),
            texture,
            textureResourceSet,
            1
        );
        DrawVertex([tl, tr, bl], source);
        DrawVertex([tr, bl, br], source);
    }

    private readonly Object vertLock = new();

    public void DrawVertex(Vertex[] v, UIWidget source)
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
