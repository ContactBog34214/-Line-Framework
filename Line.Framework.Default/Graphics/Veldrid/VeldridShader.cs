using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridShader : IShader
{
    private readonly Shader shader;

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
        switch (shader.Stage)
        {
            case ShaderStages.Vertex:
                Stage = ShaderStage.Vertex;
                break;
            case ShaderStages.Fragment:
                Stage = ShaderStage.Fragment;
                break;
            default:
                Stage = default;
                break;
        }
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
}