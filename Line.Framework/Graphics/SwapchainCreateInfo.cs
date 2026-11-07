namespace Line.Framework.Graphics
{
    public struct SwapchainCreateInfo
    {
        public SwapchainSource Source { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public PixelFormat? DepthFormat { get; set; }
        public bool SyncToVerticalBlank { get; set; }
        public bool ColorSrgb { get; set; }
    }
}
