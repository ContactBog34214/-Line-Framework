namespace Line.Framework.Graphics;

/// <summary>
/// 纹理用途：决定纹理能以什么方式被使用。
/// <para>
/// <b>必须如实声明</b>：用途不只是优化提示，后端会据此决定内存布局与校验。
/// 例如创建纹理时漏写 <see cref="Sampled"/>，之后拿它去建
/// <see cref="ResourceKind.TextureSampler"/> 会被后端直接拒绝
/// （Veldrid 与 SDL3 都会报错）。
/// </para>
/// <para>
/// 目前两个后端都只实现了 <see cref="Sampled"/> 与 <see cref="ColorTarget"/> 的
/// 完整路径；Storage 类用途尚未接入。
/// </para>
/// </summary>
[Flags]
public enum TextureUsage
{
    /// <summary>可被着色器采样（纹理 + 采样器对）。</summary>
    Sampled = 1 << 0,

    /// <summary>可作为渲染目标（离屏渲染）。</summary>
    ColorTarget = 1 << 1,

    /// <summary>可作为深度 / 模板附件。</summary>
    DepthStencilTarget = 1 << 2,

    /// <summary>可在着色器中读取（可写纹理，尚未接入）。</summary>
    StorageRead = 1 << 3,

    /// <summary>可在着色器中写入（可写纹理，尚未接入）。</summary>
    StorageWrite = 1 << 4,
}
