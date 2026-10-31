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

public partial class VeldridConverter
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

    //ShaderSet
    public static global::Veldrid.ShaderSetDescription ConvertShaderSetDescription(
        Framework.Graphics.ShaderSetDescription des
    )
    {
        return new()
        {
            Shaders =
            [
                .. des.Shaders.Select(c => (c as VeldridShader)?.shader).Where(c => c != null),
            ],
            VertexLayouts = [.. des.VertexLayouts.Select(ConvertVertexLayoutDescription)],
        };
    }
}

public partial class VeldridResourceFactory
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
