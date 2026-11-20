namespace Line.Framework.Graphics;

/// <summary>
/// 纹理采样时的过滤方式（放大与缩小通用）。
/// <para>
/// <see cref="Nearest"/> 用于像素画等需要保持硬边的场合；
/// <see cref="Linear"/> 用于普通图片与字体。
/// </para>
/// </summary>
public enum SampleFilter
{
    /// <summary>取离采样点最近的单个纹素，保持硬边。</summary>
    Nearest,

    /// <summary>对相邻纹素做线性插值，缩放时更平滑。</summary>
    Linear,
}
