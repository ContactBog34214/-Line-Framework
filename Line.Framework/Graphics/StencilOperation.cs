namespace Line.Framework.Graphics
{
    /// <summary>
    /// 模板缓冲操作：模板测试通过或失败时如何改写模板值。
    /// <para>
    /// 抽象层目前没有可用的深度 / 模板附件，因此这些值只是为了让
    /// <see cref="StencilBehaviorDescription"/> 保持完整的描述能力；
    /// 传入不会报错，但也不会有任何效果。
    /// </para>
    /// </summary>
    public enum StencilOperation : byte
    {
        /// <summary>保持原值不变。</summary>
        Keep,

        /// <summary>置为 0。</summary>
        Zero,

        /// <summary>替换为参考值。</summary>
        Replace,

        /// <summary>加一，超过最大值时停在上限。</summary>
        IncrementAndClamp,

        /// <summary>减一，低于 0 时停在 0。</summary>
        DecrementAndClamp,

        /// <summary>按位取反。</summary>
        Invert,

        /// <summary>加一，溢出后回绕到 0。</summary>
        IncrementAndWrap,

        /// <summary>减一，下溢后回绕到最大值。</summary>
        DecrementAndWrap,
    }
}
