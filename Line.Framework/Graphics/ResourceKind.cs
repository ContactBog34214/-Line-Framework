namespace Line.Framework.Graphics
{
    public enum ResourceKind
    {
        TextureSampler, // = 纹理 + 采样器（合并）
        UniformBuffer,
        StorageBufferReadOnly,
        StorageBufferReadWrite,
        StorageTexture,
    }
}
