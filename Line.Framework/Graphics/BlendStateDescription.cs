using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 整条管线的混合状态：每个颜色附件一份设置。
/// <para>
/// 抽象层目前只渲染到单个颜色目标，因此用 <see cref="SingleAlphaBlend"/> 这类
/// 单元素预置即可。若 <see cref="AttachmentStates"/> 的元素数少于附件数，
/// 后端会用第一项兜底。
/// </para>
/// </summary>
public struct BlendStateDescription
{
    /// <summary>
    /// 混合常量，供 <see cref="BlendFactor.BlendConstant"/> /
    /// <see cref="BlendFactor.InverseBlendConstant"/> 使用。
    /// </summary>
    public RgbaFloat BlendFactor { get; set; }

    /// <summary>各颜色附件的混合设置，顺序与附件的 index 对应。</summary>
    public BlendAttachmentDescription[] AttachmentStates { get; set; }

    /// <summary>
    /// alpha-to-coverage：把片元 alpha 转成多重采样的覆盖率。
    /// 与 MSAA 配合才能生效，且 SDL3 后端暂未暴露该开关。
    /// </summary>
    public bool AlphaToCoverageEnabled { get; set; }

    /// <summary>单附件：不透明覆盖。</summary>
    public static readonly BlendStateDescription SingleOverrideBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.OverrideBlend],
    };

    /// <summary>单附件：标准 alpha 混合（UI 渲染默认）。</summary>
    public static readonly BlendStateDescription SingleAlphaBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.AlphaBlend],
    };

    /// <summary>单附件：相加混合。</summary>
    public static readonly BlendStateDescription SingleAdditiveBlend = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.AdditiveBlend],
    };

    /// <summary>单附件：关闭混合。</summary>
    public static readonly BlendStateDescription SingleDisabled = new BlendStateDescription
    {
        AttachmentStates = [BlendAttachmentDescription.Disabled],
    };
}
