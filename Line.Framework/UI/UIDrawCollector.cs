using System.Numerics;
using Line.Framework.Graphics;
using Veldrid;
using Rectangle = System.Drawing.RectangleF;

namespace Line.Framework.UI;

public class UIDrawCollector
{
    /// <summary>
    /// 已提交顶点
    /// </summary>
    public virtual List<DrawCommand> Verts { get; } = [];

    /// <summary>
    /// 清除顶点
    /// </summary>
    public virtual void Clear()
    {
        Verts.Clear();
    }

    /// <summary>
    /// 绘制矩形
    /// </summary>
    /// <param name="矩形"></param>
    /// <param name="颜色"></param>
    /// <param name="源UI控件"></param>
    public virtual void DrawRect(Rectangle rect, RgbaFloat color, UIWidget source)
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

    /// <summary>
    /// 绘制带材质矩形
    /// </summary>
    /// <param name="矩形"></param>
    /// <param name="材质资产设定"></param>
    /// <param name="材质"></param>
    /// <param name="颜色"></param>
    /// <param name="源UI控件"></param>
    public virtual void DrawTexture(
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

    /// <summary>
    /// 绘制顶点
    /// </summary>
    /// <param name="顶点数组"></param>
    /// <param name="源UI控件"></param>
    public virtual void DrawVertex(Vertex[] v, UIWidget source)
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
                    Z = source.Index,
                    Source = source,
                }
            );
        }
    }
}

public class DrawCommand
{
    public Vertex[] Vert { get; set; }
    public float Z { get; set; }
    public UIWidget Source { get; set; }
}
