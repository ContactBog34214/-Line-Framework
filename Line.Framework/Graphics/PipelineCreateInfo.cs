namespace Line.Framework.Graphics;

/// <summary>
/// 创建管线的参数。
/// <para>
/// 采用「公共部分 + 类型化 Mode」的结构：资源布局对所有管线类型通用，
/// 而具体的状态描述放在 <see cref="Mode"/> 里，由后端按实际类型分发。
/// 图形管线对应 <see cref="GraphicsDescription"/>。
/// </para>
/// </summary>
public struct PipelineCreateInfo
{
    /// <summary>
    /// 资源布局数组，<b>下标即 <c>SetGraphicsResourceSet</c> 的槽位号</b>。
    /// 不使用资源时传空数组（不要传 null）。
    /// </summary>
    public IResourceLayout[] ResourceLayouts { get; set; }

    /// <summary>
    /// 类型化的管线描述。图形管线必须是 <see cref="GraphicsDescription"/>，
    /// 其他类型后端会抛 <see cref="NotSupportedException"/>。
    /// </summary>
    public IExtraCreateInfo Mode { get; set; }

    /// <summary>
    /// 管线描述的标记接口，用于让 <see cref="Mode"/> 能承载不同类型的描述。
    /// </summary>
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
