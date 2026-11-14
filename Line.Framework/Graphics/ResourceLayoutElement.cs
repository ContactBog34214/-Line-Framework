using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    public struct ResourceLayoutElement : IName
    {
        public string Name { get; set; }
        public ResourceKind Kind { get; set; }
        public ShaderStage Stages { get; set; }
    }
}
