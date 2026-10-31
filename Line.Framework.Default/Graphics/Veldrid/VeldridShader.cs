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
        var shader = dev.ResourceFactory.CreateFromSpirv(
            VeldridConverter.ConvertShaderDescription(createInfo)
        );
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
}
