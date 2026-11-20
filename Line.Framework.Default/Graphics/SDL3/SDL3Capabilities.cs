using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;
// SDL3.SDL 里有同名枚举，显式指向抽象层那个。
using PixelFormat = Line.Framework.Graphics.PixelFormat;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 后端的能力查询。
/// <para>
/// 格式与采样数<b>真正调 SDL 的探测函数</b>
/// （<see cref="SDL.GPUTextureSupportsFormat"/> /
/// <see cref="SDL.GPUTextureSupportsSampleCount"/>），
/// 不是查静态表 —— 所以结果反映的是当前驱动 + 当前设备的真实情况。
/// </para>
/// <para>
/// 颜色附件数上限是<b>写死</b>的 1：SDL3 的画面目标没有帧缓冲对象，
/// <c>BeginGPURenderPass</c> 只接受一个颜色目标，这是 API 形状的限制
/// 而非驱动能力。这一条没法探测，只能假设。
/// </para>
/// </summary>
public sealed class SDL3Capabilities : IGraphicsCapabilities
{
    private readonly SDL3Device device;

    internal SDL3Capabilities(SDL3Device device) => this.device = device;

    /// <summary>
    /// SDL3 的渲染通道只有一个颜色目标。
    /// <para>
    /// 这不是「驱动支持的附件数」，而是 <c>SDL_BeginGPURenderPass</c> 的
    /// 形状限制 —— 它在实现里只处理单个 <c>SDL_GPUColorTargetInfo</c>
    /// （本框架的 SDL3CommandList 也只构造一个）。所以是硬上限 1，
    /// 与设备能力无关。
    /// </para>
    /// </summary>
    public uint MaxColorAttachments => 1;

    /// <summary>
    /// 深度 / 模板附件可用。
    /// <para>
    /// 依赖 <c>EnsureDepthTexture</c> 按需创建深度纹理 —— 那条路径已经实现，
    /// 所以这里可以为 true。注意「能创建」不等于「深度测试一定生效」：
    /// 还需要管线用匹配的 <c>DepthStencilState</c>（框架会校验）。
    /// </para>
    /// </summary>
    public bool SupportsDepthStencil => true;

    /// <summary>
    /// true —— SDL3 后端的 mipmap 采样正常。
    /// <para>
    /// 实测证据：生成 9 层并染成洋红后，缩小显示的纹理明显变粉，
    /// 说明采样器确实落到了深层。
    /// </para>
    /// </summary>
    public bool SupportsMipmap => true;

    public bool IsTextureFormatSupported(PixelFormat format, TextureUsage usage)
    {
        // 转换失败（框架有、SDL 没有的格式）等同于「不支持」，
        // 而不是让它抛出 —— 能力查询的语义就是不要抛。
        GPUTextureFormat gpuFormat;
        try
        {
            gpuFormat = SDL3Converter.ConvertPixelFormat(format);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
        {
            return false;
        }

        var gpuUsage = SDL3Converter.ConvertTextureUsage(usage);

        // 用途为空时 ConvertTextureUsage 会返回 0，SDL 对 0 usage 的查询
        // 意义不明确（可能全 false），这里按「采样」这一最基础用途判。
        if (gpuUsage == 0)
            gpuUsage = GPUTextureUsageFlags.Sampler;

        // GPUTextureType 的成员名是 SDL_GPU_TEXTURETYPE_2D → TextureType2D，
        // 不是 D2D。可选值是 TextureType2D / 2DArray / 3D / Cube / CubeArray。
        return SDL.GPUTextureSupportsFormat(
            device.dev,
            gpuFormat,
            GPUTextureType.TextureType2D,
            gpuUsage
        );
    }

    public bool IsSampleCountSupported(PixelFormat format, SampleCount sampleCount)
    {
        GPUTextureFormat gpuFormat;
        try
        {
            gpuFormat = SDL3Converter.ConvertPixelFormat(format);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentOutOfRangeException)
        {
            return false;
        }

        return SDL.GPUTextureSupportsSampleCount(
            device.dev,
            gpuFormat,
            SDL3Converter.ConvertSampleCount(sampleCount)
        );
    }

    /// <summary>
    /// 顶点格式。
    /// <para>
    /// SDL3 的顶点格式是管线创建时静态声明的（<c>GPUVertexAttribute.Format</c>），
    /// 没有运行期探测函数，所以只能按转换表判断「框架认识这个格式」。
    /// 实际驱动是否接受属于管线创建期的事，会在建管线时暴露。
    /// </para>
    /// </summary>
    public bool IsVertexFormatSupported(VertexFormat format)
    {
        try
        {
            SDL3Converter.ConvertVertexFormat(format);
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
