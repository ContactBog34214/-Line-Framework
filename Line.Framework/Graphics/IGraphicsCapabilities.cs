using Line.Framework.Types;

namespace Line.Framework.Graphics;
/// <summary>
/// 后端能力查询：在写跨后端代码之前先问「这个后端能做吗」，
/// 而不是先做再靠捕获 <see cref="NotSupportedException"/> 来判断。
/// <para>
/// <b>为什么需要它</b>：用异常做特性探测有三个问题 ——
/// 异常分不清原因（格式不支持、附件数超限、尺寸非法都会抛同一个类型）、
/// 有性能开销（不能在每帧路径里用）、而且「抛了才发现」意味着失败发生在
/// 资源已经创建到一半的时候，清理路径容易漏。
/// </para>
/// <para>
/// <b>不追求完整</b>：这里只暴露已被抽象层用到的能力。
/// 每加一项都应该是「某个具体的公共接口需要它」驱动的，
/// 而不是「底层框架有这个字段」。
/// </para>
/// <para>
/// <b>探测 vs 假设</b>：能够真正查询的（像素格式、采样数）就查询；
/// 查不到的（如 SDL3 的单颜色附件限制）才写死，并在实现处注明是保守假设。
/// 写死的值会随框架演进而过时，注释是唯一的防线。
/// </para>
/// </summary>
public interface IGraphicsCapabilities
{
    /// <summary>
    /// 渲染目标支持的颜色附件数量上限。
    /// <para>
    /// SDL3 的渲染通道是 <b>1</b>（该后端没有帧缓冲对象，只有一个颜色目标），
    /// Veldrid 通常为 8。传超过这个数的
    /// <see cref="RenderTargetCreateInfo.ColorFormats"/> 会被拒绝。
    /// </para>
    /// </summary>
    uint MaxColorAttachments { get; }

    /// <summary>
    /// 是否支持深度 / 模板附件。
    /// <para>
    /// 两个后端目前都是 true，保留这项是为了让「将来某后端不支持」
    /// 不会变成一次破坏性变更。
    /// </para>
    /// </summary>
    bool SupportsDepthStencil { get; }

    /// <summary>
    /// 该像素格式能否作为指定用途使用。
    /// <para>
    /// 这是<b>真正向后端查询</b>的结果，不是静态表。
    /// 典型用途：判断 <see cref="PixelFormat.D32FloatS8UInt"/> 是否能做深度附件
    /// —— SDL3 官方文档明确说深度格式「二选一，不保证两者都支持」。
    /// </para>
    /// <para>
    /// 判据是「<b>全部</b>指定的用途都可用」。要分别判断多种用途就多调几次。
    /// </para>
    /// </summary>
    /// <param name="format">要检查的像素格式。</param>
    /// <param name="usage">用途标志（通常是单个标志，如 <c>TextureUsage.Sampled</c>）。</param>
    bool IsTextureFormatSupported(PixelFormat format, TextureUsage usage);

    /// <summary>
    /// 该像素格式在指定采样数下是否可用。
    /// <para>
    /// 同样真正向后端查询。用于判断能否开 MSAA ——
    /// 注意「格式支持某采样数」不等于「抽象层有 resolve 路径」，
    /// 后者目前两个后端都没有实现。
    /// </para>
    /// </summary>
    bool IsSampleCountSupported(PixelFormat format, SampleCount sampleCount);

    /// <summary>
    /// 支持的顶点输入格式。目前两个后端覆盖同一集合，
    /// 但把「某个格式在某后端不被接受」的差异留在这里，
    /// 比让管线创建时抛异常更容易排查。
    /// </summary>
    bool IsVertexFormatSupported(VertexFormat format);
}
