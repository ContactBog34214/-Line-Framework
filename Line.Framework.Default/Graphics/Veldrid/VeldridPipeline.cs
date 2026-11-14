using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid
{
    public sealed class VeldridPipeline : IPipeline
    {
        internal Pipeline Pipeline { get; }
        public PipelineType Type { get; }
        public OutputFormatDescription Outputs { get; }
        public IResourceLayout[] ResourceLayouts { get; }
        public string Name => Pipeline.Name;

        internal VeldridPipeline(Pipeline pl, PipelineCreateInfo pci)
        {
            Pipeline = pl;
            Type = pl.IsComputePipeline ? PipelineType.Compute : PipelineType.Graphics;
            ResourceLayouts = pci.ResourceLayouts;

            // 之前漏了这一行，导致 IPipeline.Outputs 永远是 default：
            // 调用方拿到的颜色附件数组是 null，用它去比对/创建 framebuffer 时会空引用。
            Outputs = pci.Mode is GraphicsDescription gd
                ? gd.Outputs
                : default(OutputFormatDescription);
        }

        public void Dispose()
        {
            Pipeline?.Dispose();
        }
    }

    public partial class VeldridConverter
    {
        /// <param name="fallbackOutputs">
        /// 当 <see cref="GraphicsDescription.Outputs"/> 未指定颜色附件时使用的回落值
        /// （通常传主交换链的 <c>Framebuffer.OutputDescription</c>）。
        /// </param>
        public static GraphicsPipelineDescription ConvertPipelineDescription(
            PipelineCreateInfo createInfo,
            global::Veldrid.OutputDescription fallbackOutputs = default
        )
        {
            if (createInfo.Mode is not GraphicsDescription gd)
                throw new NotSupportedException(
                    $"Pipeline mode {createInfo.Mode?.GetType().Name ?? "null"} is not supported "
                        + "for graphics pipelines."
                );

            if (createInfo.ResourceLayouts is null)
                throw new ArgumentException(
                    "PipelineCreateInfo.ResourceLayouts is null. "
                        + "Assign an empty array if the pipeline uses no resources.",
                    nameof(createInfo)
                );

            return new()
            {
                BlendState = ConvertBlendStateDescription(gd.BlendState),
                DepthStencilState = ConvertDepthStencilStateDescription(gd.DepthStencilState),
                RasterizerState = ConvertRasterizerStateDescription(gd.RasterizerState),
                PrimitiveTopology = ConvertPrimitiveTopology(gd.PrimitiveTopology),
                ShaderSet = ConvertShaderSetDescription(gd.ShaderSet),
                // 漏掉这一项会让 Veldrid 在 VkPipeline 构造函数里抛 NullReferenceException
                // （GraphicsPipelineDescription.Outputs.ColorAttachments 默认是 null）
                Outputs = ConvertOutputDescription(gd.Outputs, fallbackOutputs),
                ResourceLayouts =
                [
                    .. createInfo.ResourceLayouts.Select(c =>
                        c is VeldridResourceLayout rl
                            ? rl.ResourceLayout
                            : throw new ArgumentException(
                                $"Resource layout {c?.GetType().Name ?? "null"} is not a VeldridResourceLayout.",
                                nameof(createInfo)
                            )
                    ),
                ],
            };
        }

        //DepthStencilStateDescription
        public static global::Veldrid.DepthStencilStateDescription ConvertDepthStencilStateDescription(
            Framework.Graphics.DepthStencilStateDescription des
        )
        {
            return new()
            {
                DepthTestEnabled = des.DepthTestEnabled,
                DepthWriteEnabled = des.DepthWriteEnabled,
                StencilTestEnabled = des.StencilTestEnabled,
                DepthComparison = ConvertComparisonKind(des.DepthComparison),
                StencilFront = ConvertStencilBehaviorDescription(des.StencilFront),
                StencilBack = ConvertStencilBehaviorDescription(des.StencilBack),
                StencilReadMask = des.StencilReadMask,
                StencilWriteMask = des.StencilWriteMask,
                StencilReference = des.StencilReference,
            };
        }

        public static Framework.Graphics.DepthStencilStateDescription ConvertDepthStencilStateDescription(
            global::Veldrid.DepthStencilStateDescription des
        )
        {
            return new()
            {
                DepthTestEnabled = des.DepthTestEnabled,
                DepthWriteEnabled = des.DepthWriteEnabled,
                StencilTestEnabled = des.StencilTestEnabled,
                DepthComparison = ConvertComparisonKind(des.DepthComparison),
                StencilFront = ConvertStencilBehaviorDescription(des.StencilFront),
                StencilBack = ConvertStencilBehaviorDescription(des.StencilBack),
                StencilReadMask = des.StencilReadMask,
                StencilWriteMask = des.StencilWriteMask,
                StencilReference = des.StencilReference,
            };
        }

        //ComparisonKind
        public static global::Veldrid.ComparisonKind ConvertComparisonKind(CompareFunction fc)
        {
            return fc switch
            {
                CompareFunction.Never => ComparisonKind.Never,
                CompareFunction.Less => ComparisonKind.Less,
                CompareFunction.Always => ComparisonKind.Always,
                CompareFunction.Equal => ComparisonKind.Equal,
                CompareFunction.Greater => ComparisonKind.Greater,
                CompareFunction.GreaterOrEqual => ComparisonKind.GreaterEqual,
                CompareFunction.LessOrEqual => ComparisonKind.LessEqual,
                CompareFunction.NotEqual => ComparisonKind.NotEqual,
                _ => throw new ArgumentOutOfRangeException(nameof(fc), fc, null),
            };
        }

        public static CompareFunction ConvertComparisonKind(ComparisonKind fc)
        {
            return fc switch
            {
                ComparisonKind.Never => CompareFunction.Never,
                ComparisonKind.Less => CompareFunction.Less,
                ComparisonKind.Always => CompareFunction.Always,
                ComparisonKind.Equal => CompareFunction.Equal,
                ComparisonKind.Greater => CompareFunction.Greater,
                ComparisonKind.GreaterEqual => CompareFunction.GreaterOrEqual,
                ComparisonKind.LessEqual => CompareFunction.LessOrEqual,
                ComparisonKind.NotEqual => CompareFunction.NotEqual,
                _ => throw new ArgumentOutOfRangeException(nameof(fc), fc, null),
            };
        }

        //StencilOperation
        public static global::Veldrid.StencilOperation ConvertStencilOperation(
            Framework.Graphics.StencilOperation ope
        )
        {
            return ope switch
            {
                Framework.Graphics.StencilOperation.DecrementAndClamp => global::Veldrid
                    .StencilOperation
                    .DecrementAndClamp,
                Framework.Graphics.StencilOperation.DecrementAndWrap => global::Veldrid
                    .StencilOperation
                    .DecrementAndWrap,
                Framework.Graphics.StencilOperation.IncrementAndClamp => global::Veldrid
                    .StencilOperation
                    .IncrementAndClamp,
                Framework.Graphics.StencilOperation.IncrementAndWrap => global::Veldrid
                    .StencilOperation
                    .IncrementAndWrap,
                Framework.Graphics.StencilOperation.Invert => global::Veldrid
                    .StencilOperation
                    .Invert,
                Framework.Graphics.StencilOperation.Keep => global::Veldrid.StencilOperation.Keep,
                Framework.Graphics.StencilOperation.Replace => global::Veldrid
                    .StencilOperation
                    .Replace,
                Framework.Graphics.StencilOperation.Zero => global::Veldrid.StencilOperation.Zero,
                _ => throw new ArgumentOutOfRangeException(nameof(ope), ope, null),
            };
        }

        public static Framework.Graphics.StencilOperation ConvertStencilOperation(
            global::Veldrid.StencilOperation ope
        )
        {
            return ope switch
            {
                global::Veldrid.StencilOperation.DecrementAndClamp => Framework
                    .Graphics
                    .StencilOperation
                    .DecrementAndClamp,
                global::Veldrid.StencilOperation.DecrementAndWrap => Framework
                    .Graphics
                    .StencilOperation
                    .DecrementAndWrap,
                global::Veldrid.StencilOperation.IncrementAndClamp => Framework
                    .Graphics
                    .StencilOperation
                    .IncrementAndClamp,
                global::Veldrid.StencilOperation.IncrementAndWrap => Framework
                    .Graphics
                    .StencilOperation
                    .IncrementAndWrap,
                global::Veldrid.StencilOperation.Invert => Framework
                    .Graphics
                    .StencilOperation
                    .Invert,
                global::Veldrid.StencilOperation.Keep => Framework.Graphics.StencilOperation.Keep,
                global::Veldrid.StencilOperation.Replace => Framework
                    .Graphics
                    .StencilOperation
                    .Replace,
                global::Veldrid.StencilOperation.Zero => Framework.Graphics.StencilOperation.Zero,
                _ => throw new ArgumentOutOfRangeException(nameof(ope), ope, null),
            };
        }

        //StencilBehaviorDescription
        public static global::Veldrid.StencilBehaviorDescription ConvertStencilBehaviorDescription(
            Framework.Graphics.StencilBehaviorDescription des
        )
        {
            return new()
            {
                Fail = ConvertStencilOperation(des.Fail),
                Pass = ConvertStencilOperation(des.Pass),
                DepthFail = ConvertStencilOperation(des.DepthFail),
                Comparison = ConvertComparisonKind(des.Comparison),
            };
        }

        public static Framework.Graphics.StencilBehaviorDescription ConvertStencilBehaviorDescription(
            global::Veldrid.StencilBehaviorDescription des
        )
        {
            return new()
            {
                Fail = ConvertStencilOperation(des.Fail),
                Pass = ConvertStencilOperation(des.Pass),
                DepthFail = ConvertStencilOperation(des.DepthFail),
                Comparison = ConvertComparisonKind(des.Comparison),
            };
        }

        //RaterizerState
        public static Framework.Graphics.RasterizerStateDescription ConvertRasterizerStateDescription(
            global::Veldrid.RasterizerStateDescription des
        )
        {
            return new()
            {
                CullMode = ConvertFaceCullMode(des.CullMode),
                FillMode = ConvertPolygonFillMode(des.FillMode),
                FrontFace = ConvertFrontFace(des.FrontFace),
                DepthClipEnabled = des.DepthClipEnabled,
                ScissorTestEnabled = des.ScissorTestEnabled,
            };
        }

        public static global::Veldrid.RasterizerStateDescription ConvertRasterizerStateDescription(
            Framework.Graphics.RasterizerStateDescription des
        )
        {
            return new()
            {
                CullMode = ConvertFaceCullMode(des.CullMode),
                FillMode = ConvertPolygonFillMode(des.FillMode),
                FrontFace = ConvertFrontFace(des.FrontFace),
                DepthClipEnabled = des.DepthClipEnabled,
                ScissorTestEnabled = des.ScissorTestEnabled,
            };
        }

        //OutputDescription
        /// <summary>
        /// 输出附件描述。
        /// <para>
        /// 若 <paramref name="des"/> 未指定颜色附件，则回落到 <paramref name="fallback"/>
        /// （通常为交换链输出）；两者都没有时抛异常 —— 因为
        /// <see cref="global::Veldrid.GraphicsPipelineDescription.Outputs"/> 的
        /// <c>ColorAttachments</c> 为 null 会让 Veldrid 在创建管线时抛 NullReferenceException。
        /// </para>
        /// </summary>
        public static global::Veldrid.OutputDescription ConvertOutputDescription(
            OutputFormatDescription des,
            global::Veldrid.OutputDescription fallback = default
        )
        {
            if (des.ColorAttachments is not { Length: > 0 })
            {
                if (fallback.ColorAttachments is { Length: > 0 })
                    return fallback;

                throw new ArgumentException(
                    "OutputFormatDescription declares no color attachment, "
                        + "and no fallback output was supplied.",
                    nameof(des)
                );
            }

            return new()
            {
                ColorAttachments =
                [
                    .. des.ColorAttachments.Select(
                        c => new global::Veldrid.OutputAttachmentDescription(
                            ConvertPixelFormat(c.Format)
                        )
                    ),
                ],
                DepthAttachment = des.DepthAttachment.HasValue
                    ? new global::Veldrid.OutputAttachmentDescription(
                        ConvertPixelFormat(des.DepthAttachment.Value.Format)
                    )
                    : null,
                // SampleCount 枚举从 Count1 = 1 起，没有 0 值；
                // 而 default(OutputFormatDescription).SampleCount 恰好是 0。
                // 不在这里兜底的话，只填了 ColorAttachments 的调用方会收到
                // "Unsupported sample count: 0" 这种指向不明的错误。
                SampleCount = ConvertSampleCount(
                    des.SampleCount == 0 ? SampleCount.Count1 : des.SampleCount
                ),
            };
        }

        //PrimitiveTopology
        public static global::Veldrid.PrimitiveTopology ConvertPrimitiveTopology(
            Framework.Graphics.PrimitiveTopology topology
        )
        {
            return topology switch
            {
                Framework.Graphics.PrimitiveTopology.LineList => global::Veldrid
                    .PrimitiveTopology
                    .LineList,
                Framework.Graphics.PrimitiveTopology.LineStrip => global::Veldrid
                    .PrimitiveTopology
                    .LineStrip,
                Framework.Graphics.PrimitiveTopology.PointList => global::Veldrid
                    .PrimitiveTopology
                    .PointList,
                Framework.Graphics.PrimitiveTopology.TriangleList => global::Veldrid
                    .PrimitiveTopology
                    .TriangleList,
                Framework.Graphics.PrimitiveTopology.TriangleStrip => global::Veldrid
                    .PrimitiveTopology
                    .TriangleStrip,
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
            };
        }

        public static Framework.Graphics.PrimitiveTopology ConvertPrimitiveTopology(
            global::Veldrid.PrimitiveTopology topology
        )
        {
            return topology switch
            {
                global::Veldrid.PrimitiveTopology.LineList => Framework
                    .Graphics
                    .PrimitiveTopology
                    .LineList,
                global::Veldrid.PrimitiveTopology.LineStrip => Framework
                    .Graphics
                    .PrimitiveTopology
                    .LineStrip,
                global::Veldrid.PrimitiveTopology.PointList => Framework
                    .Graphics
                    .PrimitiveTopology
                    .PointList,
                global::Veldrid.PrimitiveTopology.TriangleList => Framework
                    .Graphics
                    .PrimitiveTopology
                    .TriangleList,
                global::Veldrid.PrimitiveTopology.TriangleStrip => Framework
                    .Graphics
                    .PrimitiveTopology
                    .TriangleStrip,
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
            };
        }
    }

    public partial class VeldridResourceFactory
    {
        public IPipeline CreatePipeline(PipelineCreateInfo createInfo)
        {
            if (createInfo.Mode is not GraphicsDescription)
                throw new NotSupportedException(
                    $"Mode {createInfo.Mode?.GetType().Name ?? "null"} is not supported"
                );

            // dev.MainSwapchain 只有在设备带交换链创建时才非 null。
            // 无窗口 / headless 设备（例如单元测试里的 VeldridDevice(backend)）它是 null，
            // 此时没有可回落的输出格式，必须由调用方在 GraphicsDescription.Outputs 里显式给出。
            var fallback = dev.MainSwapchain?.Framebuffer?.OutputDescription ?? default;

            var description = VeldridConverter.ConvertPipelineDescription(createInfo, fallback);

            var result = dev.ResourceFactory.CreateGraphicsPipeline(description);
            if (result == null)
                throw new InvalidOperationException("Veldrid returned a null graphics pipeline.");

            return new VeldridPipeline(result, createInfo);
        }
    }
}
