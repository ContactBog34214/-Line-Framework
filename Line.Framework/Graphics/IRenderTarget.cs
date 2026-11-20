using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 渲染目标：一次渲染往哪些纹理上写。
/// <para>
/// 这是 <c>ICommandList.SetRenderTarget</c> 的参数，也是创建设置管线时
/// 输出格式的唯一来源（<see cref="OutputFormat"/>）。
/// </para>
/// <para>
/// <b>两种来源</b>：
/// <list type="bullet">
/// <item><b>交换链目标</b>：由 <see cref="ISwapchain.RenderTarget"/> 提供，
///       呈现到窗口。它的纹理每帧由驱动轮转，<b>不可采样、不可跨帧持有</b>，
///       因此 <see cref="ColorTextures"/> 返回空列表。</item>
/// <item><b>离屏目标</b>：由 <see cref="IResourceFactory.CreateRenderTarget"/> 创建，
///       渲完可以当纹理采样（后处理、缩略图、渲染到 UI）。</item>
/// </list>
/// </para>
/// <para>
/// <b>深度</b>：有深度附件时 <see cref="DepthTexture"/> 非 null，
/// <see cref="OutputFormat"/> 的 <c>DepthAttachment</c> 也必然有值。
/// 这两个必须同时成立 —— 漏填 OutputFormat 会让管线以
/// <c>DepthStencilFormat = Invalid</c> 创建成功，然后完全不遮挡且不报错。
/// </para>
/// <para>
/// <b>同帧读写限制</b>：同一帧内，一个渲染目标写完后只能在后续的
/// render pass 里被采样，不能在同一次绘制里既写又读。
/// 后处理链天然满足这个约束。
/// </para>
/// </summary>
public interface IRenderTarget : IDisposable, IName
{
    /// <summary>宽度（像素）。</summary>
    uint Width { get; }

    /// <summary>高度（像素）。</summary>
    uint Height { get; }

    /// <summary>
    /// 输出附件格式集合。<b>这是构建管线时唯一可靠的格式来源</b>，
    /// 两个后端都有效。不要通过遍历 <see cref="ColorTextures"/> 自行拼装。
    /// </summary>
    OutputFormatDescription OutputFormat { get; }

    /// <summary>
    /// 颜色附件纹理，顺序即附件 index。
    /// <para>
    /// 交换链目标返回空列表（纹理每帧轮转且不可采样）。
    /// 离屏目标返回实际纹理，可以直接用于
    /// <see cref="ResourceBinding.FromTextureSampler"/>。
    /// </para>
    /// </summary>
    IReadOnlyList<ITexture> ColorTextures { get; }

    /// <summary>
    /// 深度纹理；null 表示没有深度附件。
    /// <para>
    /// 交换链目标的深度纹理由驱动管理，只能用于「判断是否有深度」或
    /// 采样，<b>不要 Dispose，也不要跨帧持有</b>。
    /// 离屏目标的深度纹理归目标所有，随目标一起释放。
    /// </para>
    /// </summary>
    ITexture DepthTexture { get; }
}
