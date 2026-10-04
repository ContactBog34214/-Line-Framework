namespace Line.Framework.Graphics;

public struct PipelineCreateInfo
{
    public IResourceLayout[] ResourceLayouts { get; set; }

    //独特数据值部分
    public IExtraCreateInfo Mode { get; set; }
    public interface IExtraCreateInfo;
    public struct Graphics
    {
        public ShaderSetDescription ShaderSet { get; set; }
        public VertexLayoutDescription[] VertexLayouts { get; set; }
        public PrimitiveTopology PrimitiveTopology { get; set; }
        public RasterizerStateDescription RasterizerState { get; set; }
    }
}