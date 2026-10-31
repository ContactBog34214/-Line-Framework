namespace Line.Framework.Graphics
{
    public struct StencilBehaviorDescription
    {
        public StencilOperation Fail { get; set; }
        public StencilOperation Pass { get; set; }
        public StencilOperation DepthFail { get; set; }
        public CompareFunction Comparison { get; set; }
    }
}
