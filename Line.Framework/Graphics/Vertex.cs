using System.Numerics;
using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 合成器输出的顶点（抽象层视角，尚未转成后端的紧凑结构）。
/// <para>
/// 位置为<b>屏幕像素坐标</b>，由渲染器负责换算到 NDC。
/// 纹理采样用的 UV 已经归一化，但可能带 <see cref="Coord2"/> 的缩放 / 偏移
/// （图集用），渲染器会把它折算成最终 UV。
/// </para>
/// <para>
/// 实现 <see cref="IRecyclable"/>，可通过 <see cref="Recyclable{T}"/> 复用实例；
/// 复用时务必调 <see cref="Reset"/>，否则上一次的 <see cref="Clips"/> 等状态会残留。
/// </para>
/// </summary>
public class Vertex : IRecyclable
{
    /// <summary>位置（屏幕像素坐标，左上原点）。</summary>
    public Vector2 Position { get; set; }

    /// <summary>顶点色（会与纹理采样结果相乘）。</summary>
    public Types.RgbaFloat Color { get; set; }

    /// <summary>纹理坐标（含图集缩放 / 偏移）。</summary>
    public Coord2 UV { get; set; }

    /// <summary>使用的纹理；null 表示纯色，渲染器会换成 1×1 白纹理。</summary>
    public ITexture Texture { get; set; }

    /// <summary>要绑定的资源集；null 时用渲染器的默认（白纹理）资源集。</summary>
    public IResourceSet ResourceSet { get; set; }

    /// <summary>不透明度，渲染时乘到 <see cref="Color"/> 的 alpha 上。</summary>
    public float Opacity { get; set; }

    /// <summary>
    /// 剪切区域栈（屏幕坐标的多边形顶点数组）。
    /// 每个条目是一个凸多边形，渲染时需逐个裁剪。
    /// </summary>
    public List<Vector2[]> Clips { get; set; } = new();

    /// <summary>构造一个顶点（不参与对象池）。</summary>
    public Vertex(Vector2 p, Types.RgbaFloat c, Coord2 u, ITexture t, IResourceSet rs, float o)
    {
        Position = p;
        Color = c;
        UV = u;
        Texture = t;
        ResourceSet = rs;
        Opacity = o;
    }

    /// <summary>供对象池使用，字段全为默认值。</summary>
    public Vertex() { }

    /// <summary>
    /// 从对象池取一个顶点并填充字段。
    /// </summary>
    /// <param name="clip">可选的初始剪切区域，会追加进 <see cref="Clips"/>。</param>
    public static Vertex New(
        Vector2 p,
        Types.RgbaFloat c,
        Coord2 u,
        ITexture t,
        IResourceSet rs,
        float o,
        IEnumerable<Vector2[]> clip = default
    )
    {
        var result = Recyclable<Vertex>.New();
        result.Position = p;
        result.Color = c;
        result.UV = u;
        result.Texture = t;
        result.ResourceSet = rs;
        result.Opacity = o;
        if (clip != default)
            result.Clips.AddRange(clip);
        return result;
    }

    /// <summary>
    /// 归还对象池前清空所有字段。
    /// <b>务必调用</b>，否则复用时会带着上一次的剪切区域与纹理引用。
    /// </summary>
    public void Reset()
    {
        Clips.Clear();
        Position = default;
        Color = default;
        UV = default;
        Texture = default;
        ResourceSet = default;
        Opacity = default;
    }
}
