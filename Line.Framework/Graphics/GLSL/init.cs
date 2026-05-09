using ManagedBass;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;

namespace Line.Framework.Graphics.GLSL;

public static class GLSL
{
    static byte[] vsBytes = File.ReadAllBytes("vertex.spv");
    static byte[] fsBytes = File.ReadAllBytes("fragment.spv");

    public static Shader vertex(ResourceFactory res)
    {
        return res.CreateShader(new ShaderDescription(ShaderStages.Vertex, vsBytes, "main"));
    }

    public static Shader fragment(ResourceFactory res)
    {
        return res.CreateShader(new ShaderDescription(ShaderStages.Vertex, fsBytes, "main"));
    }
}
