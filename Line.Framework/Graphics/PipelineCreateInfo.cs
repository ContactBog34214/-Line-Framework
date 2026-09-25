namespace Line.Framework.Graphics;

public struct PipelineCreateInfo
{
    public IShader[] Shaders { get; set; }
    public ISwapchain Target { get; set; }
}