namespace Line.Framework.Graphics;

public struct ShaderSetDescription
{
    public VertexLayoutDescription[] VertexLayouts { get; set; }
    public IShader Shaders { get; set; }
}