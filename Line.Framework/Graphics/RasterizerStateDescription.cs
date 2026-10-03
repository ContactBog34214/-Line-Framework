namespace Line.Framework.Graphics;

public struct RasterizerStateDescription
{
    public FaceCullMode CullMode { get; set; }
    public PolygonFillMode FillMode { get; set; }
    public FrontFace FrontFace { get; set; }
    public bool DepthClipEnabled { get; set; }
    public bool ScissorTestEnabled { get; set; }
}