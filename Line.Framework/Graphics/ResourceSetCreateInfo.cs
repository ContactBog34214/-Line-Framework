namespace Line.Framework.Graphics
{
    public struct ResourceSetCreateInfo
    {
        public IResourceLayout Layout { get; set; }
        public ITexture Texture { get; set; }

        /// null = 使用设备的默认采样器
        public ISampler Sampler { get; set; }
    }
}
