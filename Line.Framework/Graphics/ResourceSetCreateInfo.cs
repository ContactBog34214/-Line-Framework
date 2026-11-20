namespace Line.Framework.Graphics
{
    /// <summary>
    /// 创建资源集。绑定数量必须与 <see cref="Layout"/> 的
    /// <see cref="IResourceLayout.Elements"/> 一一对应（顺序即槽位）。
    /// </summary>
    public struct ResourceSetCreateInfo
    {
        /// <summary>
        /// 资源布局。绑定的槽位顺序完全由它决定
        /// （分组顺序见 <see cref="ResourceLayoutCreateInfo"/>）。
        /// </summary>
        public IResourceLayout Layout { get; set; }

        /// <summary>
        /// 按布局元素顺序给出的绑定。
        /// <para>
        /// 为空时回退到下方的单纹理字段（历史形态），
        /// 以便既有的「一个纹理 + 一个采样器」调用点无需改动。
        /// </para>
        /// </summary>
        public ResourceBinding[] Bindings { get; set; }

        /// <summary>单槽位布局（<see cref="ResourceKind.TextureSampler"/>）的简写形式。</summary>
        public ITexture Texture { get; set; }

        /// <summary>null = 使用设备的默认采样器。</summary>
        public ISampler Sampler { get; set; }

        /// <summary>
        /// 归一化出实际要绑定的列表：优先 <see cref="Bindings"/>，
        /// 否则把 <see cref="Texture"/>/<see cref="Sampler"/> 包装成单元素。
        /// </summary>
        public readonly ResourceBinding[] ResolveBindings()
        {
            if (Bindings is { Length: > 0 })
                return Bindings;

            if (Texture is not null)
                return [ResourceBinding.FromTextureSampler(Texture, Sampler)];

            return [];
        }
    }
}
