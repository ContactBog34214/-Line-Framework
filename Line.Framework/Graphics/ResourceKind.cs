namespace Line.Framework.Graphics
{
    /// <summary>
    /// 资源种类：资源布局元素与资源绑定的类型标签。
    /// <para>
    /// <b>实现状态</b>：两个后端目前只完整支持
    /// <see cref="TextureSampler"/> 与 <see cref="UniformBuffer"/>。
    /// <see cref="StorageTexture"/> 及两个 Storage buffer 种类是为将来预留的，
    /// 传入 <see cref="IResourceFactory.CreateResourceSet"/> 会抛
    /// <see cref="NotSupportedException"/>。
    /// </para>
    /// <para>
    /// 分组顺序（决定槽位编号）也由本枚举决定：采样器 → 可写纹理 →
    /// storage buffer → uniform buffer。
    /// </para>
    /// </summary>
    public enum ResourceKind
    {
        /// <summary>
        /// 纹理 + 采样器（合并成一个槽位）。
        /// <para>
        /// 后端的底层表示通常是两个独立绑定（先纹理、后采样器），
        /// 但抽象层把它当作一个元素，避免调用方手工配对。
        /// </para>
        /// </summary>
        TextureSampler,

        /// <summary>
        /// uniform buffer（常量缓冲区）。
        /// <para>
        /// 后端差异最大的一种：Veldrid 是真正的 GPU 缓冲 + 描述符绑定；
        /// SDL3 没有可绑定的 UBO，改用 CPU 影子缓冲 + 每次绑定时 push，
        /// 因此「改了数据必须重新绑定」在 SDL3 上才生效。
        /// </para>
        /// </summary>
        UniformBuffer,

        /// <summary>只读 storage buffer（结构化缓冲，尚未实现）。</summary>
        StorageBufferReadOnly,

        /// <summary>可读写 storage buffer（尚未实现）。</summary>
        StorageBufferReadWrite,

        /// <summary>可写纹理（着色器内可读写，尚未实现）。</summary>
        StorageTexture,
    }
}
