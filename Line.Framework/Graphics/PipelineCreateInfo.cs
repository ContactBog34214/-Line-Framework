namespace Line.Framework.Graphics;

public struct PipelineCreateInfo
{
    public IResourceLayout[] ResourceLayouts { get; set; }

    //独特数据值部分
    public IExtraCreateInfo Mode { get; set; }

    public interface IExtraCreateInfo;
}

public struct GraphicsDescription : PipelineCreateInfo.IExtraCreateInfo
{
    public ShaderSetDescription ShaderSet { get; set; }
    public PrimitiveTopology PrimitiveTopology { get; set; }
    public RasterizerStateDescription RasterizerState { get; set; }
    public BlendStateDescription BlendState { get; set; }
    public OutputFormatDescription Outputs { get; set; }
    public DepthStencilStateDescription DepthStencilState { get; set; }

    /// <summary>
    /// true = 顶点着色器输出的裁剪空间使用 <b>y 向上</b>（本框架的统一约定）。
    /// <para>
    /// 各后端的 NDC y 轴方向并不一致：Vulkan 原生 y 向下，
    /// Direct3D / Metal 原生 y 向上。后端负责把 shader 输出统一到本约定。
    /// </para>
    /// <list type="bullet">
    /// <item>Veldrid：创建设备时已设 <c>PreferStandardClipSpaceYDirection</c>，
    ///       此项被忽略（始终保持 y 向上）。</item>
    /// <item>SDL3：GLSL 输入会编译成 SPIR-V（只能用于 Vulkan，y 向下），
    ///       因此在编译期无条件注入 y 取反；把此项设为 false 会抛异常。</item>
    /// </list>
    /// <para>
    /// 默认 <c>true</c>（由无参构造器赋值），与框架 <c>Renderer</c> 里
    /// <c>1f - 2f * pixelY / height</c> 的换算一致。
    /// 注意 <c>default(GraphicsDescription)</c> 的该字段仍是 <c>false</c>。
    /// </para>
    /// </summary>
    public bool FlipY { get; set; }

    /// <summary>
    /// 显式构造器，把 <see cref="FlipY"/> 初始化为 <c>true</c>。
    /// struct 的属性无法声明 field initializer 而不带构造器，故如此。
    /// </summary>
    public GraphicsDescription()
    {
        FlipY = true;
    }
}
