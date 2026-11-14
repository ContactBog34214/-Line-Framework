using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

public sealed class SDL3ResourceFactory : IResourceFactory
{
    private readonly SDL3Device device;
    private ISampler defaultSampler;

    internal SDL3ResourceFactory(SDL3Device dev)
    {
        device = dev;
    }

    private IntPtr Dev => device.dev;

    /// <summary>设备默认采样器：线性过滤 + Clamp 寻址，按需创建一次。</summary>
    internal ISampler DefaultSampler =>
        defaultSampler ??= CreateSampler(
            new SamplerCreateInfo
            {
                MinFilter = SampleFilter.Linear,
                MagFilter = SampleFilter.Linear,
                MipmapMode = MipmapMode.Linear,
                AddressU = SamplerAddressMode.Clamp,
                AddressV = SamplerAddressMode.Clamp,
                AddressW = SamplerAddressMode.Clamp,
                MinLod = 0,
                MaxLod = 1,
            }
        );

    internal void DisposeOwned()
    {
        defaultSampler?.Dispose();
        defaultSampler = null;
    }

    // ---------------------------------------------------------------- Buffer

    public IDeviceBuffer CreateBuffer(BufferCreateInfo createInfo)
    {
        if (createInfo.SizeInBytes == 0)
            throw new ArgumentException(
                "BufferCreateInfo.SizeInBytes is 0 (struct default); set it explicitly.",
                nameof(createInfo)
            );
        if (createInfo.SizeInBytes > uint.MaxValue)
            throw new NotSupportedException(
                $"SDL3's GPUBuffer.Size is 32-bit; {createInfo.SizeInBytes} bytes cannot be expressed."
            );

        var info = new GPUBufferCreateInfo
        {
            Usage = SDL3Converter.ConvertBufferUsage(createInfo.Usage),
            Size = (uint)createInfo.SizeInBytes,
        };

        var handle = SDL.CreateGPUBuffer(Dev, info);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUBuffer failed: {SDL.GetError()}");

        return new SDL3Buffer(Dev, handle, createInfo.SizeInBytes, createInfo.Usage);
    }

    // ---------------------------------------------------------------- Texture

    public ITexture CreateTexture(TextureCreateInfo createInfo)
    {
        var handle = SDL.CreateGPUTexture(Dev, SDL3Converter.ConvertTextureDescription(createInfo));
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUTexture failed: {SDL.GetError()}");

        return new SDL3Texture(Dev, handle, createInfo);
    }

    public ITextureView CreateTextureView(TextureViewCreateInfo createInfo)
    {
        // SDL3 没有 texture view 概念：绑定直接使用纹理本身。
        // 这里返回一个透明包装，仅用于满足接口。
        if (createInfo.TargetTexture is null)
            throw new ArgumentNullException(nameof(createInfo.TargetTexture));

        return new SDL3TextureView(createInfo.TargetTexture);
    }

    // ---------------------------------------------------------------- Sampler

    public ISampler CreateSampler(SamplerCreateInfo createInfo)
    {
        var handle = SDL.CreateGPUSampler(Dev, SDL3Converter.ConvertSamplerDescription(createInfo));
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUSampler failed: {SDL.GetError()}");

        return new SDL3Sampler(Dev, handle);
    }

    // ---------------------------------------------------------------- Shader

    /// <summary>
    /// 创建单个着色器。
    /// <para>
    /// <b>限制</b>：SDL3 要求创建着色器时声明用了几个 sampler / UBO / storage，
    /// 而 <see cref="ShaderCreateInfo"/> 里没有这些信息。
    /// 本重载按「零资源」创建 —— 仅适用于纯色/纯顶点色着色器。
    /// 需要资源的着色器请用 <see cref="CreateShader(ShaderCreateInfo, IResourceLayout)"/>。
    /// </para>
    /// </summary>
    public IShader CreateShader(ShaderCreateInfo createInfo) =>
        CreateShader(createInfo, resourceHint: createInfo.ResourceLayout);

    /// <summary>
    /// 创建单个着色器，并从布局里统计资源数量。
    /// <para>
    /// 着色器格式按魔数自动识别：
    /// SPIR-V / DXBC 二进制直接使用；GLSL 源码会用 glslang 现编成 SPIR-V
    /// （因此构建时需要 Veldrid.SPIRV 的原生库）。
    /// </para>
    /// </summary>
    public IShader CreateShader(ShaderCreateInfo createInfo, IResourceLayout resourceHint) =>
        CreateShader(createInfo, resourceHint, flipY: false);

    /// <summary>
    /// 创建单个着色器，并从布局里统计资源数量。
    /// </summary>
    /// <param name="flipY">
    /// 保留参数，当前不参与决策。y 轴翻转统一由 viewport 完成，
    /// 见 <see cref="SDL3CommandList.SetPipeline"/>。
    /// </param>
    public IShader CreateShader(
        ShaderCreateInfo createInfo,
        IResourceLayout resourceHint,
        bool flipY
    )
    {
        if (createInfo.Bytes is not { Length: > 0 })
            throw new ArgumentException("ShaderCreateInfo.Bytes is empty.", nameof(createInfo));

        var kind = ShaderFormatSniffer.Detect(createInfo.Bytes);
        byte[] code;
        GPUShaderFormat format;

        switch (kind)
        {
            case ShaderFormatSniffer.Kind.SpirV:
                code = createInfo.Bytes;
                format = GPUShaderFormat.SPIRV;
                break;

            case ShaderFormatSniffer.Kind.Dxbc:
                code = createInfo.Bytes;
                format = GPUShaderFormat.DXBC;
                break;

            case ShaderFormatSniffer.Kind.Glsl:
                // SDL3 不接受 GLSL 源码，先编译成 SPIR-V。
                // 注意：这依赖 Veldrid.SPIRV（glslang 原生库）在运行时可用。
                //
                // 不做任何 y 轴注入。
                //
                // 实测结论：SDL3 的 Vulkan 后端已经是 y 向上（内部做了等价于
                // VK_KHR_maintenance1 负高度 viewport 的处理），与 Direct3D / Metal
                // 一致。因此两个后端对裁剪空间 y 轴的约定相同，FlipY 无需生效。
                //
                // 这也解释了为什么手动设负高度 viewport 无效 —— SDL 已经设了自己的。
                code = CompileGlslToSpirv(createInfo, flipY: false);
                format = GPUShaderFormat.SPIRV;
                break;

            case ShaderFormatSniffer.Kind.Msl:
                code = createInfo.Bytes;
                format = GPUShaderFormat.MSL;
                break;

            default:
                throw new NotSupportedException(
                    "Unrecognized shader format. Supported: SPIR-V binary, DXBC binary, "
                        + "GLSL source (compiled on the fly), MSL source. "
                        + $"First 4 bytes are {DescribeHead(createInfo.Bytes)}."
                );
        }

        uint samplers = 0,
            uniformBuffers = 0,
            storageBuffers = 0,
            storageTextures = 0;

        if (resourceHint is SDL3ResourceLayout layout)
        {
            foreach (var e in layout.Elements)
            {
                switch (e.Kind)
                {
                    case ResourceKind.TextureSampler:
                        samplers++;
                        break;
                    case ResourceKind.UniformBuffer:
                        uniformBuffers++;
                        break;
                    case ResourceKind.StorageBufferReadOnly:
                    case ResourceKind.StorageBufferReadWrite:
                        storageBuffers++;
                        break;
                    case ResourceKind.StorageTexture:
                        storageTextures++;
                        break;
                }
            }
        }

        var info = new GPUShaderCreateInfo
        {
            Format = format,
            Stage = SDL3Converter.ConvertShaderStage(createInfo.Stage),
            NumSamplers = samplers,
            NumUniformBuffers = uniformBuffers,
            NumStorageBuffers = storageBuffers,
            NumStorageTextures = storageTextures,
        };

        var entry = string.IsNullOrEmpty(createInfo.EntryPoint) ? "main" : createInfo.EntryPoint;
        var handle = SDL.CreateGPUShader(Dev, info, code, entry);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUShader failed: {SDL.GetError()}");

        return new SDL3Shader(Dev, handle, createInfo.Stage);
    }

    /// <summary>
    /// 用 glslang 把 GLSL 源码编译成 SPIR-V 字节码。
    /// </summary>
    /// <param name="flipY">
    /// 为 true 时在顶点着色器的每条 <c>gl_Position</c> 赋值语句后插入
    /// <c>gl_Position.y = -gl_Position.y;</c>。
    /// <para>
    /// <b>当前恒传 false</b>：实测 SDL3 的 Vulkan 后端已经是 y 向上
    ///（内部做了等价负高度 viewport 的处理），与 D3D / Metal 一致，
    /// 因此不需要任何翻转。此参数保留给将来可能出现的后端差异。
    /// </para>
    /// </param>
    private static byte[] CompileGlslToSpirv(ShaderCreateInfo createInfo, bool flipY)
    {
        // 去掉可能存在的 BOM，glslang 不接受
        var span = createInfo.Bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];

        string source = System.Text.Encoding.UTF8.GetString(span);

        if (flipY && createInfo.Stage == ShaderStage.Vertex)
            source = InjectFlipY(source);

        var stage =
            createInfo.Stage == ShaderStage.Vertex
                ? global::Veldrid.ShaderStages.Vertex
                : global::Veldrid.ShaderStages.Fragment;

        try
        {
            var result = global::Veldrid.SPIRV.SpirvCompilation.CompileGlslToSpirv(
                source,
                fileName: null,
                stage,
                global::Veldrid.SPIRV.GlslCompileOptions.Default
            );
            return result.SpirvBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"GLSL -> SPIR-V compilation failed (stage = {createInfo.Stage}, flipY = {flipY}): {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// <c>GraphicsDescription.FlipY</c> 在 SDL3 后端是 <b>no-op</b>。
    /// <para>
    /// 实测结论：SDL3 的 Vulkan 后端输出已经是 y 向上
    ///（内部做了等价于负高度 viewport 的处理），与 Direct3D / Metal 一致。
    /// 因此两个后端对裁剪空间 y 轴的约定相同，无需任何翻转，
    /// 该字段只作为声明性约定保留（供 <c>Renderer</c> 等上层参考）。
    /// </para>
    /// </summary>
    private static void ValidateFlipY(bool flipY)
    {
        // 有意为空：两种取值都对后端无影响。
        _ = flipY;
    }

    /// <summary>
    /// 在顶点着色器的每条 <c>gl_Position</c> 赋值语句之后插入 y 轴翻转。
    /// <para>
    /// 输出形如：
    /// <code>
    /// gl_Position = vec4(p, 0, 1);
    ///     gl_Position.y = -gl_Position.y;
    /// </code>
    /// GLSL 对空白不敏感，插入位置只需在语句之后、仍在作用域内。
    /// </para>
    /// <para>
    /// 若源码里没有任何 <c>gl_Position</c> 赋值（例如通过其他方式写出），
    /// 注入不会生效 —— 这种情况需要调用方自行处理，或改用
    /// <c>GraphicsDescription.FlipY = false</c>。
    /// </para>
    /// </summary>
    private static string InjectFlipY(string source)
    {
        const string pattern = @"gl_Position\s*=\s*[^;]+;";
        const string replacement = "$0\n    gl_Position.y = -gl_Position.y;";

        if (!System.Text.RegularExpressions.Regex.IsMatch(source, pattern))
            return source;

        return System.Text.RegularExpressions.Regex.Replace(source, pattern, replacement);
    }

    private static string DescribeHead(ReadOnlySpan<byte> bytes)
    {
        int n = Math.Min(4, bytes.Length);
        var parts = new string[n];
        for (int i = 0; i < n; i++)
            parts[i] = $"0x{bytes[i]:X2}";
        return string.Join(" ", parts);
    }

    public (IShader Vertex, IShader Fragment) CreateVertexFragmentShaders(
        ShaderCreateInfo vertex,
        ShaderCreateInfo fragment
    ) => CreateVertexFragmentShaders(vertex, fragment, flipY: false);

    /// <summary>
    /// 成对创建顶点 / 片元着色器。
    /// </summary>
    /// <param name="flipY">
    /// 保留参数，当前不参与决策：是否注入翻转宏只取决于
    /// 「顶点着色器源码里有 <c>FRAMEWORK_FLIP_Y</c> 钩子」+「驱动 y 向下」。
    /// 这样 shader 可以在 pipeline 之前创建，不必预先知道 FlipY。
    /// </param>
    public (IShader Vertex, IShader Fragment) CreateVertexFragmentShaders(
        ShaderCreateInfo vertex,
        ShaderCreateInfo fragment,
        bool flipY
    )
    {
        // flipY 只对顶点阶段有意义：片元着色器写的是颜色，不涉及 NDC 坐标。
        var vs = CreateShader(vertex, resourceHint: null, flipY: flipY);
        try
        {
            var fs = CreateShader(fragment, resourceHint: fragment.ResourceLayout);
            return (vs, fs);
        }
        catch
        {
            vs.Dispose();
            throw;
        }
    }

    // ---------------------------------------------------------------- Layout / Set

    public IResourceLayout CreateResourceLayout(ResourceLayoutCreateInfo createInfo)
    {
        if (createInfo.Elements is not { Length: > 0 })
            throw new ArgumentException(
                "ResourceLayoutCreateInfo.Elements is empty.",
                nameof(createInfo)
            );

        var snapshot = new ResourceLayoutElement[createInfo.Elements.Length];
        Array.Copy(createInfo.Elements, snapshot, snapshot.Length);

        return new SDL3ResourceLayout(snapshot);
    }

    public IResourceSet CreateResourceSet(ResourceSetCreateInfo createInfo)
    {
        if (createInfo.Layout is null)
            throw new ArgumentException(
                "ResourceSetCreateInfo.Layout is null.",
                nameof(createInfo)
            );
        if (createInfo.Texture is not SDL3Texture texture)
            throw new ArgumentException(
                $"Texture {createInfo.Texture?.GetType().Name ?? "null"} is not a SDL3Texture.",
                nameof(createInfo)
            );
        if ((createInfo.Texture.Usage & TextureUsage.Sampled) == 0)
            throw new ArgumentException(
                $"Texture was created with Usage = {createInfo.Texture.Usage}, "
                    + "which does not include Sampled.",
                nameof(createInfo)
            );

        var sampler = createInfo.Sampler ?? DefaultSampler;
        if (sampler is not SDL3Sampler s)
            throw new ArgumentException(
                $"Sampler {sampler?.GetType().Name ?? "null"} is not a SDL3Sampler.",
                nameof(createInfo)
            );

        var binding = new GPUTextureSamplerBinding { Texture = texture.handle, Sampler = s.handle };

        return new SDL3ResourceSet(createInfo.Layout, [binding]);
    }

    // ---------------------------------------------------------------- FrameBuffer

    public IFrameBuffer CreateFrameBuffer(FramebufferCreateInfo createInfo)
    {
        // SDL3 没有 framebuffer 对象：渲染通道直接以纹理为目标。
        // 离屏渲染请直接把目标纹理交给命令列表（尚未接入抽象层）。
        throw new NotSupportedException(
            "The SDL3 backend does not support explicitly created framebuffers. "
                + "SDL3's render target is the texture itself, bound directly by "
                + "BeginGPURenderPass. For swapchain targets use ISwapchain.FrameBuffer."
        );
    }

    // ---------------------------------------------------------------- Pipeline

    public IPipeline CreatePipeline(PipelineCreateInfo createInfo)
    {
        if (createInfo.Mode is not GraphicsDescription gd)
            throw new NotSupportedException(
                $"Pipeline mode {createInfo.Mode?.GetType().Name ?? "null"} is not supported."
            );
        if (createInfo.ResourceLayouts is null)
            throw new ArgumentException(
                "PipelineCreateInfo.ResourceLayouts is null (pass an empty array).",
                nameof(createInfo)
            );
        if (gd.ShaderSet.Shaders is not { Length: > 0 })
            throw new ArgumentException("ShaderSet.Shaders is empty.", nameof(createInfo));

        SDL3Shader vertex = null;
        SDL3Shader fragment = null;
        foreach (var s in gd.ShaderSet.Shaders)
        {
            if (s is not SDL3Shader sh)
                throw new ArgumentException(
                    $"Shader {s?.GetType().Name ?? "null"} is not a SDL3Shader.",
                    nameof(createInfo)
                );

            switch (sh.Stage)
            {
                case ShaderStage.Vertex:
                    vertex = sh;
                    break;
                case ShaderStage.Fragment:
                    fragment = sh;
                    break;
                default:
                    throw new NotSupportedException($"Stage {sh.Stage} not supported.");
            }
        }

        if (vertex is null || fragment is null)
            throw new ArgumentException(
                "A graphics pipeline requires both a vertex and a fragment shader.",
                nameof(createInfo)
            );

        // FlipY 只是声明性校验：SDL3 后端的 GLSL 路径已无条件注入 y 翻转
        //（见 CreateShader 的 Glsl 分支），因此这里只检查调用方声明是否自相矛盾。
        ValidateFlipY(gd.FlipY);

        // 顶点输入
        var layouts = gd.ShaderSet.VertexLayouts ?? [];
        var bufferDescs = new List<GPUVertexBufferDescription>(layouts.Length);
        var attributes = new List<GPUVertexAttribute>();

        for (uint slot = 0; slot < layouts.Length; slot++)
        {
            var vl = layouts[slot];
            if (vl.Attributes is not { Length: > 0 })
                throw new ArgumentException(
                    $"VertexLayouts[{slot}].Attributes is empty.",
                    nameof(createInfo)
                );

            bufferDescs.Add(
                new GPUVertexBufferDescription
                {
                    Slot = slot,
                    Pitch = vl.Stride,
                    InputRate =
                        vl.InputRate == VertexInputRate.Instance
                            ? GPUVertexInputRate.Instance
                            : GPUVertexInputRate.Vertex,
                    InstanceStepRate = 0,
                }
            );

            // 顺序即 location
            for (uint i = 0; i < vl.Attributes.Length; i++)
            {
                var a = vl.Attributes[i];
                attributes.Add(
                    new GPUVertexAttribute
                    {
                        Location = i,
                        BufferSlot = slot,
                        Format = SDL3Converter.ConvertVertexFormat(a.Format),
                        Offset = a.Offset,
                    }
                );
            }
        }

        // 颜色目标
        var outputs = gd.Outputs;
        if (outputs.ColorAttachments is not { Length: > 0 })
            throw new ArgumentException(
                "Outputs.ColorAttachments is empty (must be set explicitly; SDL3 has no swapchain fallback).",
                nameof(createInfo)
            );

        var blends = gd.BlendState.AttachmentStates;
        var colorTargets = new List<GPUColorTargetDescription>(outputs.ColorAttachments.Length);
        for (int i = 0; i < outputs.ColorAttachments.Length; i++)
        {
            var blend =
                blends is { Length: > 0 } && i < blends.Length
                    ? blends[i]
                    : (
                        blends is { Length: > 0 }
                            ? blends[0]
                            : BlendAttachmentDescription.AlphaBlend
                    );

            colorTargets.Add(
                SDL3Converter.ConvertColorTargetDescription(
                    blend,
                    outputs.ColorAttachments[i].Format
                )
            );
        }

        var pipelineInfo = new GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertex.handle,
            FragmentShader = fragment.handle,
            PrimitiveType = SDL3Converter.ConvertPrimitiveTopology(gd.PrimitiveTopology),
            RasterizerState = SDL3Converter.ConvertRasterizerState(gd.RasterizerState),
            MultisampleState = new GPUMultisampleState
            {
                SampleCount = SDL3Converter.ConvertSampleCount(outputs.SampleCount),
                SampleMask = 0,
                // SDL3-CS 把这个 bool 生成为 byte（按位掩码语义），0 = 不使用自定义掩码
                EnableMask = 0,
                // EnableAlphaToCoverage 在该绑定里不可访问，忽略。
                // 需要 alpha-to-coverage 时改用创建属性（Props）传递。
            },
            DepthStencilState = new GPUDepthStencilState
            {
                CompareOp = SDL3Converter.ConvertCompareOp(gd.DepthStencilState.DepthComparison),
                BackStencilState = default,
                FrontStencilState = default,
                CompareMask = gd.DepthStencilState.StencilReadMask,
                WriteMask = gd.DepthStencilState.StencilWriteMask,
                EnableDepthTest = gd.DepthStencilState.DepthTestEnabled,
                EnableDepthWrite = gd.DepthStencilState.DepthWriteEnabled,
                EnableStencilTest = gd.DepthStencilState.StencilTestEnabled,
            },
            TargetInfo = new GPUGraphicsPipelineTargetInfo
            {
                // ColorTargetDescriptions 是原生指针，不能直接赋托管数组。
                // 使用 CreateGPUGraphicsPipeline 的五参重载时，
                // 该指针会被内部替换为传入 span 的地址，这里保持 0 即可。
                ColorTargetDescriptions = IntPtr.Zero,
                NumColorTargets = (uint)colorTargets.Count,
                DepthStencilFormat = outputs.DepthAttachment.HasValue
                    ? SDL3Converter.ConvertPixelFormat(outputs.DepthAttachment.Value.Format)
                    : GPUTextureFormat.Invalid,
            },
        };

        var handle = SDL.CreateGPUGraphicsPipeline(
            Dev,
            pipelineInfo,
            bufferDescs.ToArray(),
            attributes.ToArray(),
            colorTargets.ToArray()
        );

        if (handle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SDL_CreateGPUGraphicsPipeline failed: {SDL.GetError()}"
            );

        return new SDL3Pipeline(Dev, handle, outputs, createInfo.ResourceLayouts);
    }

    // ---------------------------------------------------------------- CommandList / Swapchain

    public ICommandList CreateCommandList(string Name = null) => new SDL3CommandList(device, Name);

    public ISwapchain CreateSwapchain(SwapchainCreateInfo createInfo)
    {
        if (createInfo.Source.Surface == IntPtr.Zero)
            throw new ArgumentException(
                "SwapchainCreateInfo.Source.Surface is null. SDL3 requires an SDL_Window handle.",
                nameof(createInfo)
            );

        return new SDL3Swapchain(device, createInfo.Source.Surface, name: null, ownsClaim: true);
    }
}

/// <summary>
/// SDL3 没有 texture view：这里只是把目标纹理原样转交。
/// </summary>
public sealed class SDL3TextureView : ITextureView
{
    public ITexture TargetTexture { get; }
    public uint BaseMipLevel => 0;
    public uint MipLevels => TargetTexture.MipLevels;
    public uint BaseArrayLayer => 0;
    public uint ArrayLayers => TargetTexture.ArrayLayers;
    public string Name => "SDL3TextureView";

    internal SDL3TextureView(ITexture target) => TargetTexture = target;

    public void Dispose()
    {
        // 不拥有目标纹理
    }
}
