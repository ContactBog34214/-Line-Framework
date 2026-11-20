using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    public struct ResourceLayoutElement : IName
    {
        public string Name { get; set; }
        public ResourceKind Kind { get; set; }
        public ShaderStage Stages { get; set; }

        /// <summary>
        /// 该元素期望的资源大小（字节），仅对
        /// <see cref="ResourceKind.UniformBuffer"/> / StorageBuffer 有意义。
        /// <para>
        /// 0 = 不校验。非 0 时后端会在创建资源集时检查绑定的缓冲是否够大，
        /// 把「std140 偏移算错导致 shader 读到脏数据」这类问题提前到创建期报出来。
        /// </para>
        /// <para>
        /// 注意填的是 <b>std140 对齐后</b>的大小：struct 内每个成员按 16 字节边界对齐，
        /// 末尾补齐到 16 的倍数。例如 <c>{ mat4 Mvp; vec4 Tint; }</c> = 80。
        /// </para>
        /// </summary>
        public uint SizeInBytes { get; set; }
    }
}
