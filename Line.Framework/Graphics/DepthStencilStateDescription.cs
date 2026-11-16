namespace Line.Framework.Graphics
{
    /// <summary>
    /// 深度 / 模板测试状态。
    /// <para>
    /// <b>当前抽象层没有深度附件</b>（SDL3 交换链的深度格式为 null），
    /// 因此这些开关目前不会产生实际效果；它们保留是为了将来接深度缓冲。
    /// 需要正确遮挡时，现在的做法是 CPU 端排序后用绘制顺序表达。
    /// </para>
    /// </summary>
    public struct DepthStencilStateDescription
    {
        /// <summary>是否启用深度测试。</summary>
        public bool DepthTestEnabled { get; set; }

        /// <summary>是否写入深度值。</summary>
        public bool DepthWriteEnabled { get; set; }

        /// <summary>深度比较方式。</summary>
        public CompareFunction DepthComparison { get; set; }

        /// <summary>是否启用模板测试。</summary>
        public bool StencilTestEnabled { get; set; }

        /// <summary>正面片元的模板行为。</summary>
        public StencilBehaviorDescription StencilFront { get; set; }

        /// <summary>背面片元的模板行为。</summary>
        public StencilBehaviorDescription StencilBack { get; set; }

        /// <summary>模板读取掩码。</summary>
        public byte StencilReadMask { get; set; }

        /// <summary>模板写入掩码。</summary>
        public byte StencilWriteMask { get; set; }

        /// <summary>模板参考值。</summary>
        public uint StencilReference { get; set; }

        /// <summary>
        /// 深度测试 + 写入，比较方式 LessOrEqual。
        /// 常规不透明几何的深度设置。
        /// </summary>
        public static readonly DepthStencilStateDescription DepthOnlyLessEqual = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            DepthComparison = CompareFunction.LessOrEqual,
        };

        /// <summary>
        /// 只测不写，比较方式 LessOrEqual。
        /// 用于「已被不透明物体占用的像素不再重复绘制」的场景。
        /// </summary>
        public static readonly DepthStencilStateDescription DepthOnlyLessEqualRead = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.LessOrEqual,
        };

        /// <summary>
        /// 深度测试 + 写入，比较方式 GreaterOrEqual。
        /// 用于反转深度（近处数值更大）的管线。
        /// </summary>
        public static readonly DepthStencilStateDescription DepthOnlyGreaterEqual = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            DepthComparison = CompareFunction.GreaterOrEqual,
        };

        /// <summary>只测不写，比较方式 GreaterOrEqual。</summary>
        public static readonly DepthStencilStateDescription DepthOnlyGreaterEqualRead = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.GreaterOrEqual,
        };

        /// <summary>
        /// 完全关闭深度与模板测试。
        /// <b>2D / UI 渲染以及当前没有深度附件的管线都应当用这个</b>，
        /// 否则管线会因缺少深度附件而创建失败。
        /// </summary>
        public static readonly DepthStencilStateDescription Disabled = new()
        {
            DepthTestEnabled = false,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.LessOrEqual,
        };
    }
}
