using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 管线：把着色器、顶点布局、光栅 / 混合 / 深度状态固化成一份可绑定的对象。
/// <para>
/// 管线创建昂贵，应当长期复用；每帧重建管线会明显拖慢帧率。
/// </para>
/// </summary>
public interface IPipeline : IDisposable, IName
{
    /// <summary>管线类型（绘图或计算）。抽象层目前只实现图形管线。</summary>
    PipelineType Type { get; }

    /// <summary>
    /// 创建时声明的输出格式。
    /// 由于交换链格式由窗口决定，管线与交换链必须使用相同的格式。
    /// </summary>
    OutputFormatDescription Outputs { get; }

    /// <summary>创建时关联的资源布局，按 set 顺序排列。</summary>
    IResourceLayout[] ResourceLayouts { get; }
}
