using Line.Framework.UI;

namespace Line.Framework.Graphics;

/// <summary>
/// 合成器接口：把 UI 控件树转换成后端可直接消费的顶点流。
/// <para>
/// 它是「UI 系统」与「图形后端」之间唯一的接缝：输出与后端无关，
/// 因此同一份合成结果可以被任意后端渲染。
/// </para>
/// </summary>
public interface ICompositor
{
    /// <summary>
    /// 合成 UI 控件树的绘制数据。
    /// </summary>
    /// <param name="Root">控件树根节点。</param>
    /// <returns>
    /// 已按绘制顺序排好的顶点流（位置为屏幕像素坐标，由渲染器负责转 NDC）。
    /// 实现可以决定是否并行收集，但返回结果必须是有序的。
    /// </returns>
    Task<IEnumerable<Vertex>> Composite(UIWidget Root);
}
