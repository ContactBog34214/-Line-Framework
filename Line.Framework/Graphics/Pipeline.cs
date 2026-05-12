using Veldrid;

namespace Line.Framework.Graphics;

public static class UiPipeline
{
    // 封装所有 UI 管线创建需要的“原料”
    public record PipelineIngredients(
        VertexLayoutDescription VertexLayout,
        Shader VertexShader,
        Shader FragmentShader,
        ResourceLayout ResourceLayout
    );

    public static Pipeline Create(GraphicsDevice device, PipelineIngredients ingredients)
    {
        var factory = device.ResourceFactory;

        // 构建管线描述
        var desc = new GraphicsPipelineDescription
        {
            ShaderSet = new ShaderSetDescription(
                vertexLayouts: new[] { ingredients.VertexLayout },
                shaders: new[] { ingredients.VertexShader, ingredients.FragmentShader }
            ),

            BlendState = new BlendStateDescription(
                RgbaFloat.Black, // 全局混合因子，对常规 Alpha 混合无影响
                new BlendAttachmentDescription(
                    blendEnabled: true,
                    sourceColorFactor: BlendFactor.SourceAlpha,
                    destinationColorFactor: BlendFactor.InverseSourceAlpha,
                    colorFunction: BlendFunction.Add,
                    sourceAlphaFactor: BlendFactor.One,
                    destinationAlphaFactor: BlendFactor.InverseSourceAlpha,
                    alphaFunction: BlendFunction.Add
                )
            ),

            DepthStencilState = new DepthStencilStateDescription(
                depthTestEnabled: false,
                depthWriteEnabled: false,
                comparisonKind: ComparisonKind.Always
            ),

            RasterizerState = new RasterizerStateDescription(
                cullMode: FaceCullMode.None,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.Clockwise,
                depthClipEnabled: false,
                scissorTestEnabled: false
            ),

            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ResourceLayouts = new[] { ingredients.ResourceLayout },
            Outputs = device.MainSwapchain.Framebuffer.OutputDescription,
        };

        // 真正创建 Pipeline
        return factory.CreateGraphicsPipeline(ref desc);
    }
}
