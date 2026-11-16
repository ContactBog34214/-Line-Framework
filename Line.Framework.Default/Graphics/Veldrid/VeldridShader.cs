using Line.Framework.Graphics;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridShader : IShader
{
    internal readonly Shader shader;

    public string Name => shader.Name;

    public string EntryPoint => shader.EntryPoint;
    public ShaderStage Stage { get; }

    public void Dispose()
    {
        shader?.Dispose();
    }

    internal VeldridShader(Shader _s)
    {
        shader = _s;
        Stage = VeldridConverter.ConvertShaderStage(_s.Stage);
    }

    internal static ShaderStages GetStages(ShaderStage stage)
    {
        switch (stage)
        {
            case ShaderStage.Vertex:
                return ShaderStages.Vertex;
            case ShaderStage.Fragment:
                return ShaderStages.Fragment;
            default:
                throw new NotSupportedException();
        }
    }

    internal static ShaderStage GetStages(ShaderStages stage)
    {
        switch (stage)
        {
            case ShaderStages.Vertex:
                return ShaderStage.Vertex;
            case ShaderStages.Fragment:
                return ShaderStage.Fragment;
            default:
                throw new NotSupportedException();
        }
    }
}

public static partial class VeldridConverter
{
    //ShaderDescription
    public static global::Veldrid.ShaderDescription ConvertShaderDescription(
        ShaderCreateInfo createInfo
    )
    {
        return new()
        {
            EntryPoint = createInfo.EntryPoint,
            Stage = VeldridShader.GetStages(createInfo.Stage),
            ShaderBytes = createInfo.Bytes,
        };
    }

    public static ShaderCreateInfo ConvertShaderDescription(global::Veldrid.ShaderDescription des)
    {
        return new()
        {
            EntryPoint = des.EntryPoint,
            Stage = VeldridShader.GetStages(des.Stage),
            Bytes = des.ShaderBytes,
        };
    }

    //ShaderStage
    public static global::Veldrid.ShaderStages ConvertShaderStage(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => global::Veldrid.ShaderStages.Vertex,

            ShaderStage.Fragment => global::Veldrid.ShaderStages.Fragment,

            _ => throw new NotSupportedException($"Unsupported shader stage: {stage}"),
        };
    }

    public static ShaderStage ConvertShaderStage(global::Veldrid.ShaderStages stage)
    {
        return stage switch
        {
            global::Veldrid.ShaderStages.Vertex => ShaderStage.Vertex,

            global::Veldrid.ShaderStages.Fragment => ShaderStage.Fragment,

            _ => throw new NotSupportedException($"Unsupported Veldrid shader stage: {stage}"),
        };
    }

    public static global::Veldrid.ShaderStages ConvertShaderStages(ShaderStage stages)
    {
        const ShaderStage Known = ShaderStage.Vertex | ShaderStage.Fragment;
        if (stages == ShaderStage.None)
            throw new ArgumentException("Shader stage set must not be empty.", nameof(stages));
        if ((stages & ~Known) != 0)
            throw new ArgumentOutOfRangeException(
                nameof(stages),
                stages,
                "Contains stages unsupported by this framework."
            );
        global::Veldrid.ShaderStages result = global::Veldrid.ShaderStages.None;
        if ((stages & ShaderStage.Vertex) != 0)
            result |= global::Veldrid.ShaderStages.Vertex;
        if ((stages & ShaderStage.Fragment) != 0)
            result |= global::Veldrid.ShaderStages.Fragment;
        return result;
    }

    public static ShaderStage ConvertShaderStages(global::Veldrid.ShaderStages stages)
    {
        const global::Veldrid.ShaderStages Unsupported =
            global::Veldrid.ShaderStages.Geometry
            | global::Veldrid.ShaderStages.TessellationControl
            | global::Veldrid.ShaderStages.TessellationEvaluation
            | global::Veldrid.ShaderStages.Compute;
        if ((stages & Unsupported) != 0)
            throw new NotSupportedException(
                $"ShaderStages contains unsupported stage(s): {stages & Unsupported}."
            );
        ShaderStage result = ShaderStage.None;
        if ((stages & global::Veldrid.ShaderStages.Vertex) != 0)
            result |= ShaderStage.Vertex;
        if ((stages & global::Veldrid.ShaderStages.Fragment) != 0)
            result |= ShaderStage.Fragment;
        return result;
    }

    //ShaderSet
    /// <summary>
    /// 着色器集合 + 顶点布局。
    /// <para>
    /// Veldrid 约定 <c>Shaders[0] = vertex</c>、<c>Shaders[1] = fragment</c>，
    /// 因此这里按 <see cref="IShader.Stage"/> 归位，而不是信任传入顺序 ——
    /// 顺序传反了不会报错，只会画出完全错误的结果。
    /// </para>
    /// </summary>
    public static global::Veldrid.ShaderSetDescription ConvertShaderSetDescription(
        Framework.Graphics.ShaderSetDescription des
    )
    {
        if (des.Shaders is not { Length: > 0 })
            throw new ArgumentException(
                "ShaderSetDescription.Shaders is empty. A graphics pipeline requires "
                    + "both a vertex and a fragment shader.",
                nameof(des)
            );

        global::Veldrid.Shader vertex = null;
        global::Veldrid.Shader fragment = null;

        foreach (var s in des.Shaders)
        {
            if (s is not VeldridShader vs)
                throw new ArgumentException(
                    $"Shader {s?.GetType().Name ?? "null"} is not a VeldridShader.",
                    nameof(des)
                );

            if (vs.shader is null)
                throw new ArgumentException(
                    "Shader wrapper holds a null underlying Veldrid.Shader.",
                    nameof(des)
                );

            switch (vs.Stage)
            {
                case ShaderStage.Vertex:
                    if (vertex is not null)
                        throw new ArgumentException(
                            "ShaderSetDescription declares more than one vertex shader.",
                            nameof(des)
                        );
                    vertex = vs.shader;
                    break;

                case ShaderStage.Fragment:
                    if (fragment is not null)
                        throw new ArgumentException(
                            "ShaderSetDescription declares more than one fragment shader.",
                            nameof(des)
                        );
                    fragment = vs.shader;
                    break;

                default:
                    throw new NotSupportedException($"Shader stage {vs.Stage} is not supported.");
            }
        }

        if (vertex is null || fragment is null)
            throw new ArgumentException(
                $"ShaderSetDescription must contain both stages "
                    + $"(vertex: {(vertex is null ? "missing" : "ok")}, "
                    + $"fragment: {(fragment is null ? "missing" : "ok")}).",
                nameof(des)
            );

        if (des.VertexLayouts is null)
            throw new ArgumentException(
                "ShaderSetDescription.VertexLayouts is null. "
                    + "Assign an empty array if the pipeline binds no vertex buffers.",
                nameof(des)
            );

        return new()
        {
            // 顺序固定：0 = vertex, 1 = fragment
            Shaders = [vertex, fragment],
            VertexLayouts = [.. des.VertexLayouts.Select(ConvertVertexLayoutDescription)],
        };
    }
}

public sealed partial class VeldridResourceFactory
{
    public IShader CreateShader(ShaderCreateInfo createInfo)
    {
        var description = VeldridConverter.ConvertShaderDescription(createInfo);

        // Veldrid.SPIRV 只接受 SPIR-V 二进制。上游传 GLSL 源码时
        // （SDL3 路径自己会编）在这里补一次编译，
        // 让两个后端能吃完全相同的 ShaderCreateInfo。
        if (ShaderFormatSniffer.Detect(createInfo.Bytes) == ShaderFormatSniffer.Kind.Glsl)
        {
            // 校验：抽象层约定所有资源都在 set 0（Veldrid 的
            // ResourceBindingModel.Default 按 ResourceLayouts 下标分配 set，
            // 通常只有 set 0）。若调用方手写了别后端的 set 编号（如 SDL3 的
            // 片元采样器 set 2），这里不会报错，只会在运行时采样到 0 ——
            // 表现为「画面全黑但没有异常」，极难定位。所以主动告警。
            WarnOnNonZeroSet(createInfo);

            description = new global::Veldrid.ShaderDescription(
                description.Stage,
                CompileGlslToSpirv(createInfo),
                description.EntryPoint
            );
        }

        // 用 Veldrid 核心的 CreateShader，而不是 Veldrid.SPIRV 的单参
        // CreateFromSpirv —— 后者是 compute 专用路径，传顶点/片元会失败。
        // Vulkan 后端直接接受 SPIR-V 字节；其他后端要交叉编译时另走
        // CreateFromSpirv(vertex, fragment) 成对重载。
        var shader = dev.ResourceFactory.CreateShader(description);
        try
        {
            return new VeldridShader(shader);
        }
        catch
        {
            shader?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 检查 GLSL 里是否出现非 0 的 <c>set = N</c>，出现则警告一次。
    /// <para>
    /// 抽象层的约定是「所有资源都在 set 0」。写错不会导致编译或创建失败，
    /// 只会让资源绑不上、采样到 0（画面全黑、无异常），因此必须主动提示。
    /// 对应的另一侧：SDL3 后端会在编译前把它改写成自己的编号，
    /// 所以同一份 shader 在 SDL3 上可能反而正常，更容易误导。
    /// </para>
    /// </summary>
    private static void WarnOnNonZeroSet(ShaderCreateInfo createInfo)
    {
        if (!Line.Framework.Log.IsEnabled(Line.Framework.LogLevel.Warning))
            return;

        var span = createInfo.Bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];

        string source = System.Text.Encoding.UTF8.GetString(span);
        var m = System.Text.RegularExpressions.Regex.Match(source, @"\bset\s*=\s*([1-9]\d*)");

        if (!m.Success)
            return;

        Line.Framework.Log.Warning(
            $"[Veldrid] {createInfo.Stage} shader declares 'set = {m.Groups[1].Value}'. "
                + "This framework expects all resources in set 0 "
                + "(SDL3's backend rewrites the set number automatically; Veldrid does not). "
                + "A non-zero set will not bind and will sample as zero (black screen, no error)."
        );
    }

    /// <summary>
    /// GLSL 源码 → SPIR-V 字节码（glslang）。
    /// <para>
    /// 不做 y 轴注入：Veldrid 创建设备时已设
    /// <c>PreferStandardClipSpaceYDirection</c>，与 SDL3 的约定一致，
    /// 两边都是 y 向上，无需翻转。
    /// </para>
    /// </summary>
    private static byte[] CompileGlslToSpirv(ShaderCreateInfo createInfo)
    {
        // 去掉可能存在的 BOM，glslang 不接受
        var span = createInfo.Bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];

        string source = System.Text.Encoding.UTF8.GetString(span);

        var stage =
            createInfo.Stage == ShaderStage.Vertex
                ? global::Veldrid.ShaderStages.Vertex
                : global::Veldrid.ShaderStages.Fragment;

        try
        {
            return SpirvCompilation
                .CompileGlslToSpirv(source, fileName: null, stage, GlslCompileOptions.Default)
                .SpirvBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"GLSL -> SPIR-V compilation failed (stage = {createInfo.Stage}): {ex.Message}",
                ex
            );
        }
    }
}
