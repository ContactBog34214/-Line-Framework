namespace Line.Framework.Graphics;

public struct PipelineCreateInfo
{
    public IShader[] Shaders { get; set; }
    public IResourceLayout[] ResourceLayouts { get; set; }

    //独特数据值部分
    public IExtraCreateInfo Mode { get; set; }
    public interface IExtraCreateInfo;
    public struct Graphics
    {
        public VertexLayoutDescription[] VertexLayouts { get; set; }
        public PrimitiveTopology PrimitiveTopology { get; set; }
    }
}