using System.Numerics;
using Line.Framework.Types;
using Veldrid;

namespace Line.Framework.Graphics;

/// <summary>
/// 顶点类型
/// </summary>
public class Vertex
{
    /// <summary>
    /// 位置
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 颜色
    /// </summary>
    public Types.RgbaFloat Color { get; set; }

    /// <summary>
    /// 材质UV
    /// </summary>
    public Coord2 UV { get; set; }

    /// <summary>
    /// 材质
    /// </summary>
    public Texture Texture { get; set; }

    /// <summary>
    /// 资产设定
    /// </summary>
    public ResourceSet ResourceSet { get; set; }

    /// <summary>
    /// 透明度
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// 剪切任务表
    /// </summary>
    public List<Vector2[]> Clips { get; set; } = new();

    public Vertex(Vector2 p, Types.RgbaFloat c, Coord2 u, Texture t, ResourceSet rs, float o)
    {
        Position = p;
        Color = c;
        UV = u;
        Texture = t;
        ResourceSet = rs;
        Opacity = o;
    }
}
