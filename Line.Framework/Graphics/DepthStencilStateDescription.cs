namespace Line.Framework.Graphics
{
    public struct DepthStencilStateDescription
    {
        public bool DepthTestEnabled { get; set; }
        public bool DepthWriteEnabled { get; set; }
        public CompareFunction DepthComparison { get; set; }
        public bool StencilTestEnabled { get; set; }
        public StencilBehaviorDescription StencilFront { get; set; }
        public StencilBehaviorDescription StencilBack { get; set; }
        public byte StencilReadMask { get; set; }
        public byte StencilWriteMask { get; set; }
        public uint StencilReference { get; set; }
        public static readonly DepthStencilStateDescription DepthOnlyLessEqual = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            DepthComparison = CompareFunction.LessOrEqual,
        };

        //
        // Summary:
        //     Describes a depth-only depth stencil state which uses a Veldrid.ComparisonKind.LessEqual
        //     comparison, and disables writing to the depth buffer. The stencil test is disabled.
        //     Settings: DepthTestEnabled = true DepthWriteEnabled = false ComparisonKind =
        //     DepthComparisonKind.LessEqual
        public static readonly DepthStencilStateDescription DepthOnlyLessEqualRead = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.LessOrEqual,
        };

        //
        // Summary:
        //     Describes a depth-only depth stencil state which uses a Veldrid.ComparisonKind.GreaterEqual
        //     comparison. The stencil test is disabled. Settings: DepthTestEnabled = true DepthWriteEnabled
        //     = true ComparisonKind = DepthComparisonKind.GreaterEqual
        public static readonly DepthStencilStateDescription DepthOnlyGreaterEqual = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            DepthComparison = CompareFunction.GreaterOrEqual,
        };

        //
        // Summary:
        //     Describes a depth-only depth stencil state which uses a Veldrid.ComparisonKind.GreaterEqual
        //     comparison, and disables writing to the depth buffer. The stencil test is disabled.
        //     Settings: DepthTestEnabled = true DepthWriteEnabled = false ComparisonKind =
        //     DepthComparisonKind.GreaterEqual
        public static readonly DepthStencilStateDescription DepthOnlyGreaterEqualRead = new()
        {
            DepthTestEnabled = true,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.GreaterOrEqual,
        };

        //
        // Summary:
        //     Describes a depth-only depth stencil state in which depth testing and writing
        //     is disabled. The stencil test is disabled. Settings: DepthTestEnabled = false
        //     DepthWriteEnabled = false ComparisonKind = DepthComparisonKind.LessEqual
        public static readonly DepthStencilStateDescription Disabled = new()
        {
            DepthTestEnabled = false,
            DepthWriteEnabled = false,
            DepthComparison = CompareFunction.LessOrEqual,
        };
    }
}
