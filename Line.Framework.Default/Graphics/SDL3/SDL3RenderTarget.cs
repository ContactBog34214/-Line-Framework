using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 的离屏渲染目标。
/// <para>
/// SDL3 没有 framebuffer 对象：渲染目标就是「一组纹理」，
/// 在 <c>BeginGPURenderPass</c> 时直接交给驱动。因此本类只是
/// 纹理句柄 + 格式信息的容器，没有额外的 GPU 对象。
/// </para>
/// <para>
/// <b>单颜色附件限制</b>：SDL3 的渲染通道在本框架里只支持一个颜色目标
/// （见 <c>SDL3CommandList.EnsurePass</c>）。传多个会在创建时被拒绝，
/// 而不是静默只画第一个 —— 后者是这类错误里最难查的一种。
/// </para>
/// </summary>
public sealed class SDL3RenderTarget : IRenderTarget
{
    private readonly ITexture[] colorTextures;
    private readonly ITexture depthTexture;

    /// <summary>颜色附件 0 的原生句柄。</summary>
    internal IntPtr PrimaryColorHandle { get; }

    /// <summary>深度附件原生句柄；没有深度时为 <see cref="IntPtr.Zero"/>。</summary>
    internal IntPtr DepthHandle { get; }

    /// <summary>颜色附件纹理是否归本对象所有。提交已有纹理时为 false。</summary>
    private readonly bool ownsColorTextures;

    /// <summary>深度纹理是否归本对象所有。提交已有纹理时为 false。</summary>
    private readonly bool ownsDepthTexture;

    private int disposed;

    public string Name { get; internal set; } = "SDL3RenderTarget";

    public uint Width { get; }

    public uint Height { get; }

    public OutputFormatDescription OutputFormat { get; }

    public IReadOnlyList<ITexture> ColorTextures => colorTextures;

    public ITexture DepthTexture => depthTexture;

    internal SDL3RenderTarget(
        ITexture[] colors,
        IntPtr primaryColorHandle,
        ITexture depth,
        IntPtr depthHandle,
        bool ownsColorTextures,
        bool ownsDepthTexture
    )
    {
        this.colorTextures = colors;
        PrimaryColorHandle = primaryColorHandle;
        depthTexture = depth;
        DepthHandle = depthHandle;
        this.ownsColorTextures = ownsColorTextures;
        this.ownsDepthTexture = ownsDepthTexture;

        Width = colors[0].Width;
        Height = colors[0].Height;

        // OutputFormat 由实际附件组装，而不是从参数抄一遍：
        // 管线创建只读这个值（SDL3ResourceFactory 里 DepthStencilFormat 由它决定），
        // 漏填 DepthAttachment 会让管线以 Invalid 深度格式创建成功、
        // 深度开关也传了、但完全不遮挡且不报错。
        OutputFormat = new OutputFormatDescription
        {
            ColorAttachments =
            [
                new OutputAttachmentDescription { Format = colors[0].Format },
            ],
            DepthAttachment = depth is null
                ? null
                : new OutputAttachmentDescription { Format = depth.Format },
            SampleCount = colors[0].SampleCount,
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        if (ownsColorTextures)
            foreach (var c in colorTextures)
                c?.Dispose();

        if (ownsDepthTexture)
            depthTexture?.Dispose();
    }
}

public partial class SDL3ResourceFactory
{
    public IRenderTarget CreateRenderTarget(RenderTargetCreateInfo createInfo)
    {
        uint width = createInfo.Width;
        uint height = createInfo.Height;

        var created = new List<ITexture>();
        ITexture[] colors;
        ITexture depth = null;

        // 颜色与深度的所有权必须分开记录 —— 两者可以来自不同来源
        //（如提交已有颜色纹理、由 factory 建深度纹理）。
        bool ownsColorTextures;
        bool ownsDepthTexture = false;

        try
        {
            // ---- 颜色附件 ----
            if (createInfo.ColorTextures is { Length: > 0 } providedColors)
            {
                if (providedColors.Length > 1)
                    throw new NotSupportedException(
                        $"The SDL3 backend supports a single color attachment, "
                            + $"got {providedColors.Length}. Create one render target per target "
                            + "texture, or use the Veldrid backend for MRT."
                    );

                colors = providedColors;
                ownsColorTextures = false;

                if ((colors[0].Usage & TextureUsage.ColorTarget) == 0)
                    throw new ArgumentException(
                        $"ColorTextures[0] was created without TextureUsage.ColorTarget "
                            + $"(actual: {colors[0].Usage}).",
                        nameof(createInfo)
                    );

                if (width == 0)
                    width = colors[0].Width;
                if (height == 0)
                    height = colors[0].Height;

                if (colors[0].Width != width || colors[0].Height != height)
                    throw new ArgumentException(
                        $"ColorTextures[0] is {colors[0].Width}x{colors[0].Height} but the target "
                            + $"is {width}x{height}.",
                        nameof(createInfo)
                    );
            }
            else
            {
                if (createInfo.ColorFormats is not { Length: > 0 } formats)
                    throw new ArgumentException(
                        "RenderTargetCreateInfo has neither ColorTextures nor ColorFormats; "
                            + "there is nothing to render into.",
                        nameof(createInfo)
                    );
                if (formats.Length > 1)
                    throw new NotSupportedException(
                        $"The SDL3 backend supports a single color attachment, "
                            + $"got {formats.Length} in ColorFormats."
                    );
                if (width == 0 || height == 0)
                    throw new ArgumentException(
                        $"RenderTargetCreateInfo needs explicit Width/Height when creating "
                            + $"textures from formats, got {width}x{height}.",
                        nameof(createInfo)
                    );

                var tex = CreateTexture(
                    new TextureCreateInfo
                    {
                        Width = width,
                        Height = height,
                        Format = formats[0],
                        // Sampled 必须一起声明：离屏渲染的主要用途就是渲完再采样。
                        Usage = TextureUsage.ColorTarget | TextureUsage.Sampled,
                        SampleCount = createInfo.SampleCount,
                        // 渲染目标不申请 mip：只上传 0 级会留下未初始化的后续层。
                        MipLevels = 1,
                        ArrayLayers = 1,
                    }
                );
                created.Add(tex);
                colors = [tex];
                ownsColorTextures = true;
            }

            // ---- 深度附件 ----
            if (createInfo.DepthTexture is { } providedDepth)
            {
                depth = providedDepth;
                if ((depth.Usage & TextureUsage.DepthStencilTarget) == 0)
                    throw new ArgumentException(
                        $"DepthTexture was created without TextureUsage.DepthStencilTarget "
                            + $"(actual: {depth.Usage}).",
                        nameof(createInfo)
                    );
                if (depth.Width != width || depth.Height != height)
                    throw new ArgumentException(
                        $"DepthTexture is {depth.Width}x{depth.Height} but the target is "
                            + $"{width}x{height}; all attachments must match.",
                        nameof(createInfo)
                    );
            }
            else if (createInfo.DepthFormat is { } depthFormat)
            {
                depth = CreateTexture(
                    new TextureCreateInfo
                    {
                        Width = width,
                        Height = height,
                        Format = depthFormat,
                        Usage = TextureUsage.DepthStencilTarget,
                        SampleCount = createInfo.SampleCount,
                        MipLevels = 1,
                        ArrayLayers = 1,
                    }
                );
                created.Add(depth);
                ownsDepthTexture = true;
            }

            var colorHandle = ToHandle(colors[0], nameof(createInfo));
            var depthHandle = depth is null ? IntPtr.Zero : ToHandle(depth, nameof(createInfo));

            return new SDL3RenderTarget(
                colors,
                colorHandle,
                depth,
                depthHandle,
                ownsColorTextures,
                ownsDepthTexture
            );
        }
        catch
        {
            // 已创建的纹理必须回滚，否则失败路径会持续泄漏显存。
            foreach (var t in created)
                t?.Dispose();
            throw;
        }
    }

    private static IntPtr ToHandle(ITexture texture, string paramName)
    {
        if (texture is not SDL3Texture st)
            throw new ArgumentException(
                $"Render target attachment {texture?.GetType().Name ?? "null"} "
                    + "is not a SDL3Texture.",
                paramName
            );
        return st.handle;
    }
}
