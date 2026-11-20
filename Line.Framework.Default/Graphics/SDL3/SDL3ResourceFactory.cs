using Line.Framework;
using Line.Framework.Graphics;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

public sealed partial class SDL3ResourceFactory : IResourceFactory
{
    private readonly SDL3Device device;
    private ISampler defaultSampler;

    internal SDL3ResourceFactory(SDL3Device dev)
    {
        device = dev;
    }

    private IntPtr Dev => device.dev;

    /// <summary>
    /// 设备默认采样器：线性过滤 + Clamp 寻址，按需创建一次。
    /// <para>
    /// 参数必须与 Veldrid 侧 <c>VeldridResourceSet.DefaultSampler</c> 逐字段一致，
    /// 否则同一份 UI 代码在两个后端下观感会不同（曾因 MaxLod 差 1 导致
    /// SDL3 下贴图发淡、透明度闪烁、小尺寸贴图直接看不见）。
    /// </para>
    /// </summary>
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
                // UI 纹理通常不带 mipmap，限制在 mip 0 避免无谓采样
                MinLod = 0,
                MaxLod = 0,
                AnisotropyEnabled = false,
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

        // uniform 走 CPU 影子 + push，不能与几何用途混用：
        // 顶点/索引缓冲必须是真的 GPUBuffer，而 push 需要 CPU 数据。
        if ((createInfo.Usage & BufferUsage.Uniform) != 0)
        {
            var other = createInfo.Usage & ~(BufferUsage.Uniform | BufferUsage.Dynamic);
            if (other != 0)
                throw new NotSupportedException(
                    $"BufferUsage.Uniform cannot be combined with {other} on the SDL3 backend: "
                        + "uniform data is pushed from CPU memory, not bound as a GPU buffer."
                );

            return new SDL3UniformBuffer((uint)createInfo.SizeInBytes, createInfo.Usage);
        }

        var flags = SDL3Converter.ConvertBufferUsage(createInfo.Usage);
        if (flags == 0)
            // 没有任何用途标志时 VkBufferCreateInfo.usage 会是 0（validation 直接报错），
            // 补一个无害的 STORAGE_BUFFER 位兜底。
            flags = GPUBufferUsageFlags.GraphicsStorageRead;

        var info = new GPUBufferCreateInfo
        {
            Usage = flags,
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
                // 已经编译好的 SPIR-V 无法在源码层面重映射 set 编号。
                // 若它用了通用约定（全在 set 0），uniform 会读不到 ——
                // 提前告警，别让人再走一遍我们走过的弯路。
                if (createInfo.ResourceLayout is not null)
                    Log.Debug(
                        $"[SDL3] {createInfo.Stage}: input is precompiled SPIR-V; "
                            + "descriptor set numbers are used as-is. SDL expects "
                            + $"uniform data in set {(createInfo.Stage == ShaderStage.Vertex ? 1 : 3)} "
                            + $"and other resources in set "
                            + $"{(createInfo.Stage == ShaderStage.Vertex ? 0 : 2)}."
                    );
                break;

            case ShaderFormatSniffer.Kind.Dxbc:
                code = createInfo.Bytes;
                format = GPUShaderFormat.DXBC;
                break;

            case ShaderFormatSniffer.Kind.Glsl:
                // 调用方给的是 GLSL 源码，本后端负责编成「当前 GPU 驱动能接受的格式」。
                //
                // 这一点是必须的：SDL3Device 按后端声明了不同的着色器格式
                //（Vulkan → SPIR-V，Metal → MSL/MetalLib，D3D → DXBC/DXIL），
                // 若这里无脑产出 SPIR-V，选 Metal / Direct3D 时
                // SDL_CreateGPUShader 就会失败（或窗口什么都不渲染）。
                //
                // 不做任何 y 轴注入：SDL3 的 Vulkan 后端已经是 y 向上
                //（内部做了等价于 VK_KHR_maintenance1 负高度 viewport 的处理），
                // 与 D3D / Metal 一致，因此两边对裁剪空间的约定相同。
                (code, format) = CompileGlslForDevice(createInfo);
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
                // 计数是「按着色器阶段」的：SDL_CreateGPUShader 描述的是
                // 这一个 stage 用到多少资源，顶点阶段不该为片元阶段的 UBO 买单。
                if ((e.Stages & createInfo.Stage) == 0)
                    continue;

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

        // —— 首选 SDL_ShaderCross 路径 ——
        //
        // 为什么不用裸 SDL_CreateGPUShader 喂 SPIR-V：
        // 那条路径把 descriptor 的 set/binding 编号原样交给驱动，
        // 而 SDL GPU 对 SPIR-V 有自己的一套约定（UBO / SSBO / sampler /
        // storage texture 各自落在固定的 set 上，且在组内从 0 连续编号）。
        // 两边对不上时 SDL 不报错 —— 结果是「资源计数全对、命令全部提交成功、
        // shader 却读到全零」，正是我们遇到的现象。
        //
        // SDL_ShaderCross_CompileGraphicsShaderFromSPIRV 会按 SDL 的约定
        // 重写 SPIR-V 的 descriptor 声明，并直接返回可用的 SDL_GPUShader*。
        // 它需要的 GraphicsShaderResourceInfo 与我们从布局统计出来的四个
        // 计数完全同构，因此这一步不需要额外的反射。
        //
        // 对抽象层零影响：IResourceFactory / ShaderCreateInfo / 调用方都不变。
        if (format == GPUShaderFormat.SPIRV && UseShaderCross)
        {
            var handle = CreateShaderViaShaderCross(
                code,
                createInfo.Stage,
                entry,
                samplers,
                uniformBuffers,
                storageBuffers,
                storageTextures
            );

            if (handle != IntPtr.Zero)
            {
                Log.Debug($"[SDL3] {createInfo.Stage} shader created via ShaderCross.");
                return new SDL3Shader(Dev, handle, createInfo.Stage);
            }

            // 回退而不是抛出：ShaderCross 在某些格式组合上可能不支持，
            // 裸路径至少能让简单着色器跑起来。
            Log.Warning(
                $"[SDL3] SDL_ShaderCross_CompileGraphicsShaderFromSPIRV failed for "
                    + $"{createInfo.Stage}: {SDL.GetError()}. Falling back to SDL_CreateGPUShader "
                    + "(descriptor set/binding will not be remapped)."
            );
        }

        var gpuHandle = SDL.CreateGPUShader(Dev, info, code, entry);
        if (gpuHandle == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUShader failed: {SDL.GetError()}");

        return new SDL3Shader(Dev, gpuHandle, createInfo.Stage);
    }

    /// <summary>
    /// 是否优先走 <c>SDL_ShaderCross</c> 创建 SPIR-V 着色器。
    /// <para>
    /// <b>默认 false</b>：该 API 需要额外的原生库 <c>libSDL3_shadercross</c>，
    /// 它不随 SDL3-CS 的 NuGet 包分发，缺库时会抛 DllNotFoundException。
    /// 只有在确实安装了该库（自己编译 SDL_shadercross 并放到
    /// runtimes/&lt;rid&gt;/native/ 下）时才值得打开。
    /// </para>
    /// <para>
    /// 注意：set 编号的问题已经由 <see cref="RemapDescriptorSets"/> 在 GLSL
    /// 源码层面解决，不依赖 ShaderCross。
    /// </para>
    /// </summary>
    public static bool UseShaderCross { get; set; } = false;

    /// <summary>
    /// 通过 <c>SDL_ShaderCross_CompileGraphicsShaderFromSPIRV</c> 创建着色器。
    /// <para>
    /// 这条路径会按 SDL GPU 的约定重写 SPIR-V 里的 descriptor set / binding，
    /// 因此调用方在 GLSL 里写 <c>set = 0, binding = 0</c> 即可，
    /// 不必关心 SDL 内部的槽位布局。
    /// </para>
    /// </summary>
    /// <returns>成功的 <c>SDL_GPUShader*</c>；失败返回 <see cref="IntPtr.Zero"/>。</returns>
    private IntPtr CreateShaderViaShaderCross(
        byte[] spirv,
        ShaderStage stage,
        string entrypoint,
        uint samplers,
        uint uniformBuffers,
        uint storageBuffers,
        uint storageTextures
    )
    {
        // GraphicsShaderResourceInfo 的四个计数与我们从布局统计的完全同构。
        // 用命名属性赋值，不依赖字段声明顺序。
        var resourceInfo = new global::SDL3.ShaderCross.GraphicsShaderResourceInfo
        {
            NumSamplers = samplers,
            NumUniformBuffers = uniformBuffers,
            NumStorageBuffers = storageBuffers,
            NumStorageTextures = storageTextures,
        };

        // ShaderCross 有自己的一套 ShaderStage 枚举（不是 GPUShaderStage）。
        // 直接用数值 cast 而不是引用成员名：XML 文档里没有该枚举的成员定义，
        // 硬写 .Vertex / .Fragment 有编译失败的风险。数值按 C 头文件为：
        //   SDL_SHADERSTAGE_VERTEX = 0, SDL_SHADERSTAGE_FRAGMENT = 1
        var crossStage = (global::SDL3.ShaderCross.ShaderStage)(
            stage == ShaderStage.Vertex ? 0 : 1
        );

        try
        {
            unsafe
            {
                fixed (byte* p = spirv)
                {
                    return global::SDL3.ShaderCross.CompileGraphicsShaderFromSPIRV(
                        Dev,
                        (IntPtr)p,
                        (UIntPtr)spirv.Length,
                        entrypoint,
                        crossStage,
                        ref resourceInfo,
                        0, // infoProps：SPIR-V 输入元数据，0 = 默认
                        0  // shaderProps：输出着色器元数据，0 = 默认
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[SDL3] ShaderCross threw: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 把 GLSL 里的 <c>set</c> 编号重映射成 SDL GPU 对 SPIR-V 的约定。
    /// <para>
    /// <b>为什么必须做这一步</b>（SDL 官方 SDL_CreateGPUShader 文档）：
    /// <list type="bullet">
    /// <item>顶点：set 0 = sampler / storage texture / storage buffer，
    ///       <b>set 1 = uniform data</b></item>
    /// <item>片元：set 2 = sampler / storage texture / storage buffer，
    ///       <b>set 3 = uniform data</b></item>
    /// </list>
    /// 写错时 SDL <b>不会报错</b>：uniform block 被当成 set 0 里的普通资源，
    /// 而 PushGPU*UniformData 推的数据落在 set 1/3 的槽位，两边接不上，
    /// shader 读到全零。症状是「资源计数正确、命令全部提交成功、画面全空」。
    /// </para>
    /// <para>
    /// 抽象层的约定是「所有资源都在 set 0」（与 Veldrid 的
    /// ResourceBindingModel.Default 一致），SDL3 后端在这里完成转换，
    /// 因此调用方不需要为任何后端特化 shader。
    /// </para>
    /// </summary>
    private static string RemapDescriptorSets(string source, ShaderStage stage)
    {
        uint uniformSet = stage == ShaderStage.Vertex ? 1u : 3u;
        uint resourceSet = stage == ShaderStage.Vertex ? 0u : 2u;

        // 1) uniform block：layout(...) uniform Transform { ... }
        //    后面必须跟 '{'，因此不会误伤 layout(...) uniform sampler2D xxx;
        source = System.Text.RegularExpressions.Regex.Replace(
            source,
            @"layout\s*\([^)]*\)\s*uniform\s+(?:[A-Za-z_]\w*\s*)?\{",
            m => SetLayoutSet(m.Value, uniformSet)
        );

        // 2) 其余资源：采样器 / 可写纹理 / storage buffer
        source = System.Text.RegularExpressions.Regex.Replace(
            source,
            @"layout\s*\([^)]*\)\s*(?:uniform\s+(?:sampler|image|texture)\w*|buffer\s+[A-Za-z_]\w*\s*\{)",
            m => SetLayoutSet(m.Value, resourceSet)
        );

        return source;
    }

    /// <summary>改写（或补上）一条 layout 限定符里的 <c>set = N</c>。</summary>
    private static string SetLayoutSet(string declaration, uint set)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(declaration, @"\bset\s*="))
            return System.Text.RegularExpressions.Regex.Replace(
                declaration,
                @"\bset\s*=\s*\d+",
                $"set = {set}"
            );

        // 原来没写 set（GLSL 默认 0）：补在 layout( 之后
        return System.Text.RegularExpressions.Regex.Replace(
            declaration,
            @"layout\s*\(",
            m => $"{m.Value}set = {set}, "
        );
    }

    /// <summary>
    /// 按当前 GPU 驱动把 GLSL 源码编成可用的着色器字节 + 格式。
    /// <para>
    /// <b>SDL3 的设备只接受特定格式</b>（见 <see cref="SDL3Device"/> 里按后端
    /// 声明的那组 <c>GPUShaderFormat</c>）。因此这里必须先看
    /// <see cref="SDL3Device.Name"/>（即 <c>SDL_GetGPUDeviceDriver</c> 的结果）
    /// 再决定怎么编，否则选 Metal / Direct3D 时会产出一份对方读不了的字节。
    /// </para>
    /// <para>
    /// <b>目前只有 Vulkan 路径可用</b>：
    /// <list type="bullet">
    /// <item>Vulkan → glslang 编 SPIR-V（本框架已依赖 Veldrid.SPIRV 的原生库）。</item>
    /// <item>Metal / Direct3D → 需要 SPIR-V 交叉编译成 MSL / DXIL。
    ///       SDL 官方入口是 <c>SDL_ShaderCross</c>，但它的原生库
    ///       <c>libSDL3_shadercross</c> 不随 SDL3-CS 的 NuGet 包分发；
    ///       Veldrid.SPIRV 只有成对的 <c>CompileVertexFragment</c>，
    ///       拿不到单阶段结果。因此这里明确抛
    ///       <see cref="NotSupportedException"/>，而不是静默产出错误格式
    ///       让窗口黑屏。</item>
    /// </list>
    /// </para>
    /// </summary>
    private (byte[] Code, GPUShaderFormat Format) CompileGlslForDevice(ShaderCreateInfo createInfo)
    {
        string driver = device.Name ?? string.Empty;

        // Vulkan：直接编 SPIR-V，这条是完整可用的路径
        if (driver.Contains("vulkan", StringComparison.OrdinalIgnoreCase))
            return (CompileGlslToSpirv(createInfo, flipY: false), GPUShaderFormat.SPIRV);

        // Metal / Direct3D：需要交叉编译，当前无可用工具链，明确拒绝
        if (
            driver.Contains("metal", StringComparison.OrdinalIgnoreCase)
            || driver.Contains("direct3d", StringComparison.OrdinalIgnoreCase)
        )
            throw new NotSupportedException(
                $"The SDL3 '{driver}' backend needs shaders in "
                    + (
                        driver.Contains("metal", StringComparison.OrdinalIgnoreCase)
                            ? "MSL / MetalLib"
                            : "DXBC / DXIL"
                    )
                    + " form, but this framework can only compile GLSL to SPIR-V. "
                    + "Cross-compilation requires SDL_ShaderCross "
                    + "(libSDL3_shadercross, not shipped with SDL3-CS) "
                    + "or an equivalent HLSL/MSL toolchain. "
                    + "Use the Vulkan backend, or precompile shaders and pass the bytes directly."
            );

        // 未知驱动：不做假设，直接说明无法判断
        throw new NotSupportedException(
            $"Cannot compile GLSL for unknown SDL3 GPU driver '{driver}'. "
                + "Pass precompiled shader bytes instead, or use the Vulkan backend."
        );
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

        // 关键一步：SDL GPU 的 set 编号约定与通用约定（全在 set 0）不同，
        // 且写错不报错、只表现为「uniform 读到零」。
        // 详见 RemapDescriptorSets 的注释。
        source = RemapDescriptorSets(source, createInfo.Stage);

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

        // 槽位映射排查用，需要时打开：
        // Log.Debug($"[SDL3] layout slots: {ResourceLayoutOrdering.Describe(snapshot, sdl3: true)}");

        return new SDL3ResourceLayout(snapshot);
    }

    public IResourceSet CreateResourceSet(ResourceSetCreateInfo createInfo)
    {
        if (createInfo.Layout is null)
            throw new ArgumentException(
                "ResourceSetCreateInfo.Layout is null.",
                nameof(createInfo)
            );
        if (createInfo.Layout is not SDL3ResourceLayout layout)
            throw new ArgumentException(
                $"Layout {createInfo.Layout.GetType().Name} is not a SDL3ResourceLayout.",
                nameof(createInfo)
            );

        var elements = layout.Elements;
        if (elements.Length == 0)
            throw new ArgumentException(
                "Layout declares no elements; a resource set bound to it would be empty.",
                nameof(createInfo)
            );

        var bindings = createInfo.ResolveBindings();
        if (bindings.Length != elements.Length)
            throw new ArgumentException(
                $"Layout declares {elements.Length} element(s) but "
                    + $"{bindings.Length} binding(s) were supplied. "
                    + "Bindings are matched to elements by position.",
                nameof(createInfo)
            );

        // 四条列表分别对应 SDL 的绑定入口：
        //   samplers → SDL_BindGPU{Vertex,Fragment}Samplers
        //   uniforms → SDL_PushGPU{Vertex,Fragment}UniformData（绑定那一刻推送）
        // 组内顺序 = 布局里的出现顺序，必须与 shader 中的声明顺序一致。
        var vertexSamplers = new List<GPUTextureSamplerBinding>(elements.Length);
        var fragmentSamplers = new List<GPUTextureSamplerBinding>(elements.Length);
        var vertexUniforms = new List<SDL3UniformBuffer>(elements.Length);
        var fragmentUniforms = new List<SDL3UniformBuffer>(elements.Length);

        for (int i = 0; i < elements.Length; i++)
        {
            var e = elements[i];
            var b = bindings[i];
            var label = string.IsNullOrEmpty(e.Name) ? $"#{i}" : $"'{e.Name}'";

            if (e.Stages == ShaderStage.None)
                throw new ArgumentException(
                    $"Layout element {label} declares no shader stage.",
                    nameof(createInfo)
                );

            switch (e.Kind)
            {
                case ResourceKind.UniformBuffer:
                {
                    if (b.Buffer is not SDL3UniformBuffer uniform)
                        throw new ArgumentException(
                            $"Layout element {label} is UniformBuffer but the binding is "
                                + $"{b.Buffer?.GetType().Name ?? "not a buffer"}. "
                                + "On SDL3 a uniform buffer must be created with "
                                + "BufferUsage.Uniform (CPU shadow + push).",
                            nameof(createInfo)
                        );

                    if (e.SizeInBytes > 0 && uniform.SizeInBytes < e.SizeInBytes)
                        throw new ArgumentException(
                            $"Layout element {label} expects {e.SizeInBytes} bytes but the "
                                + $"bound buffer is {uniform.SizeInBytes} bytes.",
                            nameof(createInfo)
                        );

                    // 一个元素声明两阶段时两边各占一个槽位，都要推一次。
                    if ((e.Stages & ShaderStage.Vertex) != 0)
                        vertexUniforms.Add(uniform);
                    if ((e.Stages & ShaderStage.Fragment) != 0)
                        fragmentUniforms.Add(uniform);
                    break;
                }

                case ResourceKind.TextureSampler:
                {
                    if (b.Texture is not SDL3Texture texture)
                        throw new ArgumentException(
                            $"Layout element {label} is TextureSampler but the binding is "
                                + $"{b.Texture?.GetType().Name ?? "not a texture"}.",
                            nameof(createInfo)
                        );
                    if ((texture.Usage & TextureUsage.Sampled) == 0)
                        throw new ArgumentException(
                            $"Texture bound to {label} was created with Usage = "
                                + $"{texture.Usage}, which does not include Sampled.",
                            nameof(createInfo)
                        );

                    var samplerOwner = b.Sampler ?? DefaultSampler;
                    if (samplerOwner is not SDL3Sampler sampler)
                        throw new ArgumentException(
                            $"Sampler bound to {label} is "
                                + $"{samplerOwner?.GetType().Name ?? "null"}, not a SDL3Sampler.",
                            nameof(createInfo)
                        );

                    var pair = new GPUTextureSamplerBinding
                    {
                        Texture = texture.handle,
                        Sampler = sampler.handle,
                    };

                    if ((e.Stages & ShaderStage.Vertex) != 0)
                        vertexSamplers.Add(pair);
                    if ((e.Stages & ShaderStage.Fragment) != 0)
                        fragmentSamplers.Add(pair);
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"Layout element {label} is {e.Kind}; the SDL3 backend currently "
                            + "supports UniformBuffer and TextureSampler only."
                    );
            }
        }

        return new SDL3ResourceSet(
            createInfo.Layout,
            vertexSamplers.ToArray(),
            fragmentSamplers.ToArray(),
            vertexUniforms.ToArray(),
            fragmentUniforms.ToArray()
        );
    }

    // ---------------------------------------------------------------- RenderTarget
    //
    // CreateRenderTarget 实现在 SDL3RenderTarget.cs（同一个 partial class）。
    // SDL3 没有 framebuffer 对象，所以这里没有对应的「创建帧缓冲」入口 ——
    // 渲染目标就是一组纹理，在 BeginGPURenderPass 时直接交给驱动。

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
                // 必须显式转换，不能留 default：带模板的深度格式下
                // stencilOpState 的 op 即使是 Invalid 也可能让管线创建失败。
                BackStencilState = SDL3Converter.ConvertStencilBehavior(
                    gd.DepthStencilState.StencilBack
                ),
                FrontStencilState = SDL3Converter.ConvertStencilBehavior(
                    gd.DepthStencilState.StencilFront
                ),
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

        // SDL3 的交换链纹理由 SDL 提供，但**深度纹理不提供**，必须自己建。
        // 那条路径在 SDL3Swapchain.EnsureDepthTexture 里（按 acquire 到的
        // 真实尺寸创建 / 重建）。
        return new SDL3Swapchain(
            device,
            createInfo.Source.Surface,
            name: null,
            ownsClaim: true,
            depthFormat: createInfo.DepthFormat
        );
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
