namespace Line.Framework.Graphics;

/// <summary>
/// 创建纹理的参数。
/// <para>
/// 只有 <see cref="Width"/>、<see cref="Height"/> 与 <see cref="Usage"/> 必须显式给出；
/// 其余字段留默认值时后端会按「二维、单层、单 mip、单采样」处理。
/// </para>
/// </summary>
public struct TextureCreateInfo
{
    /// <summary>宽度（纹素）。</summary>
    public uint Width { get; set; }

    /// <summary>高度（纹素）。一维纹理留 0 或 1。</summary>
    public uint Height { get; set; }

    /// <summary>数组层数；0 视为 1。</summary>
    public uint ArrayLayers { get; set; }

    /// <summary>mip 层数；0 视为 1。</summary>
    public uint MipLevels { get; set; }

    /// <summary>深度；0 视为 1（仅三维纹理需要）。</summary>
    public uint Depth { get; set; }

    /// <summary>维度与排布形式；默认 <see cref="TextureType.Texture2D"/>。</summary>
    public TextureType Type { get; set; }

    /// <summary>像素格式。</summary>
    public PixelFormat Format { get; set; }

    /// <summary>多重采样数；0 视为 <see cref="SampleCount.Count1"/>。</summary>
    public SampleCount SampleCount { get; set; }

    /// <summary>
    /// 用途，决定这块纹理之后能被怎样使用。
    /// <b>必须包含后续绑定所需的用途</b>（如作为采样纹理时要有
    /// <see cref="TextureUsage.Sampled"/>），否则绑定时会被拒绝。
    /// </summary>
    public TextureUsage Usage { get; set; }
}
