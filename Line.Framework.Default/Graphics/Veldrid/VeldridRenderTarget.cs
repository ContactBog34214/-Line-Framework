using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 的渲染目标。
/// <para>
/// <b>两种形态</b>：
/// <list type="bullet">
/// <item><b>交换链目标</b>：包装驱动提供的 <c>Framebuffer</c>，
///       不拥有它（Resize / present 后驱动会换新实例）。
///       <see cref="ColorTextures"/> 返回空列表 —— 交换链纹理轮转且不可采样。</item>
/// <item><b>离屏目标</b>：持有由 factory 创建的 <c>Framebuffer</c>，
///       释放时一并释放它；纹理是否归本对象所有由创建方式决定。</item>
/// </list>
/// </para>
/// </summary>
public sealed class VeldridRenderTarget : IRenderTarget
{
    internal readonly global::Veldrid.Framebuffer framebuffer;

    /// <summary>是否拥有底层 Framebuffer（交换链的由驱动管理）。</summary>
    private readonly bool ownsFramebuffer;

    /// <summary>颜色附件纹理是否归本对象所有（提交已有纹理时为 false）。</summary>
    private readonly bool ownsColorTextures;

    /// <summary>深度纹理是否归本对象所有（提交已有纹理时为 false）。</summary>
    private readonly bool ownsDepthTexture;

    private readonly ITexture[] colorTextures;
    private readonly ITexture depthTexture;
    private int disposed;

    /// <summary>
    /// 调试标签。交换链的 framebuffer 通常没有名字，
    /// 这里兜底成空串，保持 <see cref="Line.Framework.Types.IName"/> 的非空约定。
    /// </summary>
    public string Name => framebuffer.Name ?? string.Empty;

    public uint Width => framebuffer.Width;

    public uint Height => framebuffer.Height;

    /// <summary>
    /// 输出格式。取自 framebuffer 自身而不是创建参数 ——
    /// 后者在交换链路径下根本不存在，而在离屏路径下也可能与
    /// 驱动的实际选择不同。framebuffer 是权威来源。
    /// </summary>
    public OutputFormatDescription OutputFormat { get; }

    public IReadOnlyList<ITexture> ColorTextures => colorTextures;

    public ITexture DepthTexture => depthTexture;

    /// <summary>
    /// 交换链目标的构造器：包一个驱动提供的 Framebuffer，不拥有任何资源。
    /// <para>
    /// 交换链带深度时（<see cref="SwapchainCreateInfo.DepthFormat"/> 非 null），
    /// 这里把深度纹理包装成不拥有的 <see cref="VeldridTexture"/> 暴露出去 ——
    /// 调用方需要它来判断能否清深度，也可能需要它做深度贴图效果。
    /// </para>
    /// </summary>
    internal VeldridRenderTarget(
        global::Veldrid.Framebuffer fb,
        global::Veldrid.GraphicsDevice gd
    )
    {
        framebuffer = fb ?? throw new ArgumentNullException(nameof(fb));
        ownsFramebuffer = false;
        ownsColorTextures = false;
        ownsDepthTexture = false;
        colorTextures = [];
        OutputFormat = VeldridConverter.ConvertOutputDescription(fb.OutputDescription);

        // Framebuffer.DepthTarget 是「附加了哪张纹理的哪个 mip/layer」，
        // 包装成纹理时 owns = false：交换链的资源由驱动轮转，
        // 调用方即使对这个 ITexture 调 Dispose 也不会破坏交换链。
        depthTexture = fb.DepthTarget is { } dt && gd is not null
            ? new VeldridTexture(dt.Target, gd, owns: false)
            : null;
    }

    /// <summary>
    /// 离屏目标的构造器。<paramref name="colors"/> 与 <paramref name="depth"/>
    /// 是最终生效的附件（可能是新创建的，也可能是调用方提交的）。
    /// <para>
    /// 颜色与深度的所有权<b>必须分开</b>：允许「提交已有颜色纹理 + 让 factory
    /// 建深度纹理」这种组合，此时只拥有深度。合并成一个标志会导致
    /// Dispose 时释放掉调用方的纹理。
    /// </para>
    /// </summary>
    internal VeldridRenderTarget(
        global::Veldrid.Framebuffer fb,
        IReadOnlyList<ITexture> colors,
        ITexture depth,
        bool ownsColorTextures,
        bool ownsDepthTexture
    )
    {
        framebuffer = fb ?? throw new ArgumentNullException(nameof(fb));
        ownsFramebuffer = true;
        this.ownsColorTextures = ownsColorTextures;
        this.ownsDepthTexture = ownsDepthTexture;
        colorTextures = [.. colors];
        depthTexture = depth;
        OutputFormat = VeldridConverter.ConvertOutputDescription(fb.OutputDescription);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        // 顺序：先释放 framebuffer（它引用纹理），再释放拥有的纹理。
        if (ownsFramebuffer)
            framebuffer?.Dispose();

        if (ownsColorTextures)
            foreach (var c in colorTextures)
                c?.Dispose();

        if (ownsDepthTexture)
            depthTexture?.Dispose();
    }
}

public partial class VeldridResourceFactory
{
    public IRenderTarget CreateRenderTarget(RenderTargetCreateInfo createInfo)
    {
        uint width = createInfo.Width;
        uint height = createInfo.Height;

        var created = new List<ITexture>();
        ITexture[] colors;
        ITexture depth;

        // 颜色与深度的所有权必须分开记录 —— 两者可以来自不同来源。
        bool ownsColorTextures;
        bool ownsDepthTexture = false;

        try
        {
            // ---- 颜色附件 ----
            if (createInfo.ColorTextures is { Length: > 0 } providedColors)
            {
                // 提交已有纹理：不拥有。尺寸留 0 时从第一个纹理推导。
                colors = providedColors;
                ownsColorTextures = false;
                if (width == 0)
                    width = colors[0].Width;
                if (height == 0)
                    height = colors[0].Height;

                for (int i = 0; i < colors.Length; i++)
                {
                    if ((colors[i].Usage & TextureUsage.ColorTarget) == 0)
                        throw new ArgumentException(
                            $"ColorTextures[{i}] was created without TextureUsage.ColorTarget "
                                + $"(actual: {colors[i].Usage}).",
                            nameof(createInfo)
                        );
                    if (colors[i].Width != width || colors[i].Height != height)
                        throw new ArgumentException(
                            $"ColorTextures[{i}] is {colors[i].Width}x{colors[i].Height} but the "
                                + $"target is {width}x{height}; all attachments must match.",
                            nameof(createInfo)
                        );
                }
            }
            else
            {
                if (createInfo.ColorFormats is not { Length: > 0 } formats)
                    throw new ArgumentException(
                        "RenderTargetCreateInfo has neither ColorTextures nor ColorFormats; "
                            + "there is nothing to render into.",
                        nameof(createInfo)
                    );
                if (width == 0 || height == 0)
                    throw new ArgumentException(
                        $"RenderTargetCreateInfo needs explicit Width/Height when creating "
                            + $"textures from formats, got {width}x{height}.",
                        nameof(createInfo)
                    );

                var list = new ITexture[formats.Length];
                for (int i = 0; i < formats.Length; i++)
                {
                    list[i] = CreateTexture(
                        new TextureCreateInfo
                        {
                            Width = width,
                            Height = height,
                            Format = formats[i],
                            // Sampled 必须一起声明：渲染目标最常见的用途就是
                            // 渲完当纹理采样，漏了会在 CreateResourceSet 时被拒。
                            Usage = TextureUsage.ColorTarget | TextureUsage.Sampled,
                            SampleCount = createInfo.SampleCount,
                            // 渲染目标不申请 mip：mip 层只上传 0 级会得到未初始化内容。
                            MipLevels = 1,
                            ArrayLayers = 1,
                        }
                    );
                    created.Add(list[i]);
                }

                colors = list;
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
            else
            {
                depth = null;
            }

            // ---- 底层 Framebuffer ----
            // 用对象初始化器而不是构造器：ArrayLayer / MipLevel 是「附加哪一层 /
            // 哪一级」的索引，不是数量。用属性名写清楚比位置参数安全。
            var fbDescription = new global::Veldrid.FramebufferDescription
            {
                ColorTargets =
                [
                    .. colors.Select(c => new global::Veldrid.FramebufferAttachmentDescription
                    {
                        Target = ToVeldridTexture(c, nameof(createInfo)),
                        ArrayLayer = 0,
                        MipLevel = 0,
                    }),
                ],
                DepthTarget =
                    depth is null
                        ? null
                        : new global::Veldrid.FramebufferAttachmentDescription
                        {
                            Target = ToVeldridTexture(depth, nameof(createInfo)),
                            ArrayLayer = 0,
                            MipLevel = 0,
                        },
            };

            var fb = dev.ResourceFactory.CreateFramebuffer(fbDescription);
            return new VeldridRenderTarget(
                fb,
                colors,
                depth,
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

    private static global::Veldrid.Texture ToVeldridTexture(ITexture texture, string paramName)
    {
        if (texture is not VeldridTexture vt)
            throw new ArgumentException(
                $"Render target attachment {texture?.GetType().Name ?? "null"} "
                    + "is not a VeldridTexture.",
                paramName
            );
        return vt.texture;
    }
}
