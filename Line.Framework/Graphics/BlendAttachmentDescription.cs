namespace Line.Framework.Graphics
{
    /// <summary>
    /// 单个颜色附件的混合设置。
    /// <para>
    /// 颜色与 alpha 两条通道分别有各自的因子与运算，因此可以做出
    /// 「RGB 相加、alpha 覆盖」这类组合。UI 渲染通常用 <see cref="AlphaBlend"/>。
    /// </para>
    /// </summary>
    public struct BlendAttachmentDescription
    {
        /// <summary>是否启用混合；false 时颜色直接覆盖目标。</summary>
        public bool BlendEnabled { get; set; }

        /// <summary>源颜色的混合因子。</summary>
        public BlendFactor SourceColorFactor { get; set; }

        /// <summary>目标颜色的混合因子。</summary>
        public BlendFactor DestinationColorFactor { get; set; }

        /// <summary>
        /// 颜色写入掩码；null 表示由后端取默认值（全部通道）。
        /// </summary>
        public ColorWriteMask? ColorWriteMask { get; set; }

        /// <summary>源 alpha 的混合因子。</summary>
        public BlendFactor SourceAlphaFactor { get; set; }

        /// <summary>目标 alpha 的混合因子。</summary>
        public BlendFactor DestinationAlphaFactor { get; set; }

        /// <summary>颜色通道的混合运算。</summary>
        public BlendOperation ColorFunction { get; set; }

        /// <summary>alpha 通道的混合运算。</summary>
        public BlendOperation AlphaFunction { get; set; }

        /// <summary>不透明覆盖：源 ×1 + 目标 ×0，即直接盖住目标。</summary>
        public static readonly BlendAttachmentDescription OverrideBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.One,
                DestinationColorFactor = BlendFactor.Zero,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.One,
                DestinationAlphaFactor = BlendFactor.Zero,
                AlphaFunction = BlendOperation.Add,
            };

        /// <summary>
        /// 标准 alpha 混合：源 × 源 alpha + 目标 ×(1 − 源 alpha)。
        /// 本框架 UI 渲染使用的就是这一种。
        /// </summary>
        public static readonly BlendAttachmentDescription AlphaBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.SourceAlpha,
                DestinationColorFactor = BlendFactor.InverseSourceAlpha,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.SourceAlpha,
                DestinationAlphaFactor = BlendFactor.InverseSourceAlpha,
                AlphaFunction = BlendOperation.Add,
            };

        /// <summary>相加混合：源 × 源 alpha + 目标 ×1，用于发光 / 光晕等叠加效果。</summary>
        public static readonly BlendAttachmentDescription AdditiveBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.SourceAlpha,
                DestinationColorFactor = BlendFactor.One,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.SourceAlpha,
                DestinationAlphaFactor = BlendFactor.One,
                AlphaFunction = BlendOperation.Add,
            };

        /// <summary>关闭混合（源直接覆盖，alpha 通道也直接写入）。</summary>
        public static readonly BlendAttachmentDescription Disabled = new BlendAttachmentDescription
        {
            BlendEnabled = false,
            SourceColorFactor = BlendFactor.One,
            DestinationColorFactor = BlendFactor.Zero,
            ColorFunction = BlendOperation.Add,
            SourceAlphaFactor = BlendFactor.One,
            DestinationAlphaFactor = BlendFactor.Zero,
            AlphaFunction = BlendOperation.Add,
        };
    }
}
