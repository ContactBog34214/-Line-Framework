namespace Line.Framework.Graphics;

/// <summary>
/// 创建离屏渲染目标的参数。
/// <para>
/// <b>两种使用方式</b>：
/// <list type="bullet">
/// <item><b>按格式创建</b>：只给 <see cref="ColorFormats"/> /
///       <see cref="DepthFormat"/>，factory 自己建纹理并拥有它们，
///       目标释放时一并释放。这是最常用的方式。</item>
/// <item><b>复用已有纹理</b>：给 <see cref="ColorTextures"/>（可再给
///       <see cref="DepthTexture"/>），目标<b>不拥有</b>这些纹理，
///       释放目标不会释放它们。适用于渲染目标池、或把已有纹理
///       临时作为渲染目标。</item>
/// </list>
/// </para>
/// <para>
/// <b>优先级</b>：<see cref="ColorTextures"/> 非空时忽略
/// <see cref="ColorFormats"/>；<see cref="DepthTexture"/> 非空时忽略
/// <see cref="DepthFormat"/>。两组至少要有其一，否则没有可写目标。
/// </para>
/// <para>
/// <b>关于尺寸</b>：<see cref="ColorTextures"/> 非空时 <see cref="Width"/> /
/// <see cref="Height"/> 可以留 0，此时从第一个颜色纹理推导。
/// 显式给出时必须与纹理尺寸一致，否则后端会拒绝（附件尺寸必须统一）。
/// </para>
/// </summary>
public struct RenderTargetCreateInfo
{
    /// <summary>宽度（像素）。提交已有纹理且尺寸一致时可留 0。</summary>
    public uint Width { get; set; }

    /// <summary>高度（像素）。提交已有纹理且尺寸一致时可留 0。</summary>
    public uint Height { get; set; }

    /// <summary>
    /// 直接使用的颜色纹理（顺序即附件 index）。
    /// <para>
    /// 非空时目标<b>不拥有</b>它们 —— 释放目标不会释放纹理。
    /// 纹理必须已声明 <see cref="TextureUsage.ColorTarget"/>，
    /// 想渲完再采样还要有 <see cref="TextureUsage.Sampled"/>，
    /// 否则后端在绑定或创建管线时拒绝。
    /// </para>
    /// </summary>
    public ITexture[] ColorTextures { get; set; }

    /// <summary>
    /// 直接使用的深度纹理。
    /// <para>
    /// 非空时目标<b>不拥有</b>它。纹理必须已声明
    /// <see cref="TextureUsage.DepthStencilTarget"/>。
    /// </para>
    /// </summary>
    public ITexture DepthTexture { get; set; }

    /// <summary>
    /// 由 factory 创建颜色纹理时使用的格式，顺序即附件 index。
    /// 仅在 <see cref="ColorTextures"/> 为空时生效。
    /// </summary>
    public PixelFormat[] ColorFormats { get; set; }

    /// <summary>
    /// 由 factory 创建深度纹理时使用的格式；null = 不带深度附件。
    /// 仅在 <see cref="DepthTexture"/> 为空时生效。
    /// <para>
    /// 需要深度测试时非 null，且创建的管线必须用对应的
    /// <see cref="DepthStencilStateDescription"/>（不能是
    /// <c>Disabled</c>，否则格式对不上会创建失败）。
    /// </para>
    /// </summary>
    public PixelFormat? DepthFormat { get; set; }

    /// <summary>
    /// 多重采样数。0 视为 <see cref="SampleCount.Count1"/>。
    /// 提交已有纹理时必须与纹理的采样数一致。
    /// </summary>
    public SampleCount SampleCount { get; set; }
}
