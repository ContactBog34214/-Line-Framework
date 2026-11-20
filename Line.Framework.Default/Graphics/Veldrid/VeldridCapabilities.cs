using Line.Framework.Graphics;
using PixelFormat = Line.Framework.Graphics.PixelFormat;
using TextureUsage = Line.Framework.Graphics.TextureUsage;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 后端的能力查询。
/// <para>
/// 格式与采样数走 <c>GraphicsDevice.GetPixelFormatSupport</c> 的<b>真实探测</b>：
/// Veldrid 会为每种格式填一个 <c>PixelFormatProperties</c>（含
/// <c>IsSampleCountSupported</c>），因此结果反映驱动实际能力。
/// </para>
/// <para>
/// 颜色附件数上限是<b>写死</b>的 8：Veldrid 没有暴露这个查询。
/// 8 是 Vulkan 规范要求的最小保证值（<c>maxColorAttachments</c> 必须 ≥ 8），
/// 所以按它下界假设不会低估到出错，最坏情况是低估了真实能力。
/// </para>
/// </summary>
public sealed class VeldridCapabilities : IGraphicsCapabilities
{
    private readonly global::Veldrid.GraphicsDevice device;

    internal VeldridCapabilities(global::Veldrid.GraphicsDevice device) => this.device = device;

    /// <summary>
    /// 颜色附件数上限。
    /// <para>
    /// Veldrid 不提供 <c>maxColorAttachments</c> 查询，这里取 Vulkan 规范的
    /// 最小保证值 8。D3D11 同样是 8。宁可低估也不要在跨后端代码里
    /// 依赖一个未被验证的上限。
    /// </para>
    /// </summary>
    public uint MaxColorAttachments => 8;

    /// <summary>
    /// 深度 / 模板附件可用。
    /// <para>
    /// Veldrid 的交换链与 <c>CreateFramebuffer</c> 都原生支持深度附件，
    /// 且不需要额外分配（与 SDL3 不同），所以为 true。
    /// </para>
    /// </summary>
    public bool SupportsDepthStencil => true;

    /// <summary>
    /// <b>false —— Veldrid 后端的 mipmap 采样不可用。</b>
    /// <para>
    /// 已经验证过的事实：纹理能创建 9 层、mip1..8 数据能上传、
    /// 视图覆盖全部 mip（<c>texelFetch(tex, ivec2(0,0), 8)</c> 能读到第 8 层）、
    /// 采样器参数为 <c>MinLinear_MagLinear_MipLinear</c>、<c>MaxLod=32</c>。
    /// 但 <c>texture()</c> 始终只返回 mip0，即使把
    /// <c>MinLod</c>/<c>MaxLod</c> 双双设为 3、或直接
    /// <c>textureLod(uv, 3.0)</c> 也不变。
    /// </para>
    /// <para>
    /// 结论：Veldrid 把我们的 sampler 与 <c>vkCreateSampler</c> 之间有一层
    /// 未生效的映射，具体位置未查明。这是已知限制，不抛异常。
    /// </para>
    /// <para>
    /// 声明为 false 后，<c>RResourceSet</c> 不再生成 mip 链 ——
    /// 省掉 CPU 降采样和那份额外 33% 的显存。需要 mipmap 时请用 SDL3 后端。
    /// </para>
    /// </summary>
    public bool SupportsMipmap => false;

    public bool IsTextureFormatSupported(PixelFormat format, TextureUsage usage)
    {
        global::Veldrid.PixelFormat veldridFormat;
        try
        {
            veldridFormat = VeldridConverter.ConvertPixelFormat(format);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
        {
            // 框架有、Veldrid 没有的格式 → 不支持，而不是抛。
            return false;
        }

        var textureUsage = VeldridConverter.ConvertTextureUsage(usage);

        // 用途为 0 时 ConvertTextureUsage 返回 0，Veldrid 的探测对这种查询
        // 没有明确语义。取最基础的 Sampled 作为「这个格式能不能用」的判据。
        if (textureUsage == 0)
            textureUsage = global::Veldrid.TextureUsage.Sampled;

        // GetPixelFormatSupport 返回 bool，属性通过 out 带出。
        // 需要同时给出 TextureType：同一格式在不同类型下支持情况可能不同。
        return device.GetPixelFormatSupport(
            veldridFormat,
            global::Veldrid.TextureType.Texture2D,
            textureUsage,
            out _
        );
    }

    public bool IsSampleCountSupported(PixelFormat format, SampleCount sampleCount)
    {
        global::Veldrid.PixelFormat veldridFormat;
        try
        {
            veldridFormat = VeldridConverter.ConvertPixelFormat(format);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
        {
            return false;
        }

        if (
            !device.GetPixelFormatSupport(
                veldridFormat,
                global::Veldrid.TextureType.Texture2D,
                global::Veldrid.TextureUsage.Sampled,
                out var properties
            )
        )
            return false;

        return properties.IsSampleCountSupported(
            VeldridConverter.ConvertSampleCount(sampleCount)
        );
    }

    /// <summary>
    /// 顶点格式。
    /// <para>
    /// Veldrid 没有运行期探测顶点格式的接口，只能按转换表判断
    /// 「框架认识这个格式」。Half1 这类 Veldrid 独有或框架不支持的格式
    /// 会在转换时抛出，据此返回 false。
    /// </para>
    /// </summary>
    public bool IsVertexFormatSupported(VertexFormat format)
    {
        try
        {
            VeldridConverter.ConvertVertexFormat(format);
            return true;
        }
        // 转换表用 switch 兜底抛 ArgumentOutOfRangeException，
        // 不是 NotSupportedException —— 两个都要接。
        catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
