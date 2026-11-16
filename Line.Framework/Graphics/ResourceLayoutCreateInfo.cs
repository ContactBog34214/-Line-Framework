namespace Line.Framework.Graphics
{
    /// <summary>
    /// 创建资源布局。
    /// <para>
    /// <b>槽位编号规则</b>（由后端统一实现，调用方无需关心）：
    /// 元素按 <see cref="ResourceLayoutElement.Kind"/> 分组，组的先后固定为
    /// 采样器 → 可写纹理 → storage buffer → uniform buffer；
    /// 组内按 <see cref="Elements"/> 里的出现顺序从 0 连续编号。
    /// </para>
    /// <para>
    /// 因此：<b>同类资源之间的先后有意义</b>（决定 binding 编号），
    /// <b>不同类之间的先后无意义</b>（会被重排）。
    /// 写 shader 时按「分组顺序 + 组内顺序」编号即可，
    /// 两个后端（Veldrid 单一 descriptor set、SDL3 按阶段分 set）会得到相同结果。
    /// </para>
    /// </summary>
    public struct ResourceLayoutCreateInfo
    {
        public ResourceLayoutElement[] Elements { get; set; }
    }
}
