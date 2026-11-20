namespace Line.Framework.Graphics
{
    /// <summary>
    /// 模板测试的单个面（正面或背面）的行为。
    /// <para>
    /// 模板测试有三条分支，各自指定一个改写操作：
    /// 模板测试失败 / 模板通过但深度失败 / 全部通过。
    /// </para>
    /// </summary>
    public struct StencilBehaviorDescription
    {
        /// <summary>模板测试失败时对模板值的操作。</summary>
        public StencilOperation Fail { get; set; }

        /// <summary>模板测试与深度测试都通过时的操作。</summary>
        public StencilOperation Pass { get; set; }

        /// <summary>模板测试通过但深度测试失败时的操作。</summary>
        public StencilOperation DepthFail { get; set; }

        /// <summary>模板比较方式。</summary>
        public CompareFunction Comparison { get; set; }
    }
}
