namespace Line.Framework.Graphics;

/// <summary>
/// 多重采样数（MSAA 的每像素采样个数）。
/// <para>
/// <see cref="Count1"/> 表示关闭多重采样，是本框架目前的默认值。
/// 注意各后端支持的上限不同：SDL3 只支持到 8，因此超过 8 的取值
/// 会由后端在转换时抛异常而不是静默降级。
/// </para>
/// </summary>
public enum SampleCount
{
    /// <summary>不启用多重采样。</summary>
    Count1 = 1,

    /// <summary>每像素 2 个采样。</summary>
    Count2 = 2,

    /// <summary>每像素 4 个采样。</summary>
    Count4 = 4,

    /// <summary>每像素 8 个采样（SDL3 的上限）。</summary>
    Count8 = 8,

    /// <summary>每像素 16 个采样（SDL3 不支持）。</summary>
    Count16 = 16,

    /// <summary>每像素 32 个采样（SDL3 不支持）。</summary>
    Count32 = 32,

    /// <summary>每像素 64 个采样（SDL3 不支持）。</summary>
    Count64 = 64,
}
