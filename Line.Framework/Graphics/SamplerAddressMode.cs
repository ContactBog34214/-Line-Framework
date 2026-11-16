namespace Line.Framework.Graphics;

/// <summary>
/// 纹理坐标超出 [0,1] 时的寻址方式。
/// <para>
/// UI 渲染应当用 <see cref="Clamp"/> —— 用 <see cref="Wrap"/> 时，
/// 图集或带边缘留白的纹理在采样到边界外会渗出对侧像素，
/// 表现为图片边缘出现一条细缝。
/// </para>
/// </summary>
public enum SamplerAddressMode : byte
{
    /// <summary>环绕重复（平铺纹理用）。</summary>
    Wrap,

    /// <summary>镜像重复。</summary>
    Mirror,

    /// <summary>钳制到边缘像素（UI 默认）。</summary>
    Clamp,

    /// <summary>
    /// 返回预定义的边界颜色，由
    /// <see cref="SamplerDescription.BorderColor"/> 指定。
    /// </summary>
    Border,
}
