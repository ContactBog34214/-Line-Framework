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
        }

        public void Dispose()
        {
            Pipeline?.Dispose();
        }
    }

    public partial class VeldridConverter
    {
        public static GraphicsPipelineDescription ConvertPipelineDescription(
            PipelineCreateInfo createInfo
        )
        {
            if (createInfo.Mode is GraphicsDescription gd)
            {
                return new()
                {
                    BlendState = ConvertBlendStateDescription(gd.BlendState),
                    DepthStencilState = ConvertDepthStencilStateDescription(gd.DepthStencilState),
                    RasterizerState = ConvertRasterizerStateDescription(gd.RasterizerState),
                    PrimitiveTopology = ConvertPrimitiveTopology(gd.PrimitiveTopology),
                    ShaderSet = ConvertShaderSetDescription(gd.ShaderSet),
                };
            }
            return default;
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
            global::Veldrid.Pipeline Result = null;
            if (createInfo.Mode is GraphicsDescription)
            {
                Result = dev.ResourceFactory.CreateGraphicsPipeline(
                    VeldridConverter.ConvertPipelineDescription(createInfo)
                );
            }
            if (Result != null)
                return new VeldridPipeline(Result, createInfo);
            throw new NotSupportedException(
                $"Mode {createInfo.Mode.GetType().Name} is not supported"
            );
        }
    }
}
