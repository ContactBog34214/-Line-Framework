namespace Line.Framework.Graphics;

/// <summary>
/// 着色器阶段。
/// <para>
/// 声明为 <see cref="FlagsAttribute"/>，因为资源布局元素需要表达
/// 「同一资源被多个阶段共用」（如同时被顶点与片元读取的 uniform buffer）。
/// 单个 <c>ShaderCreateInfo.Stage</c> 则必须恰好是一位 —— 构造时会校验。
/// </para>
/// </summary>
[Flags]
public enum ShaderStage
{
    /// <summary>未指定（资源布局中表示错误，会被后端拒绝）。</summary>
    None = 0,

    /// <summary>顶点着色器：处理每个顶点，输出裁剪空间坐标。</summary>
    Vertex = 1 << 0,

    /// <summary>片元（像素）着色器：处理每个片元，输出颜色。</summary>
    Fragment = 1 << 1,
}
