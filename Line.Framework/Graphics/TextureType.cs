namespace Line.Framework.Graphics;

/// <summary>
/// 纹理维度与排布形式。
/// <para>
/// 数值取自显式的 1~7 而非默认自增，方便与后端枚举一一对照时排查错位。
/// 本框架的 2D 渲染只用 <see cref="Texture2D"/>。
/// </para>
/// </summary>
public enum TextureType : uint
{
    /// <summary>一维纹理（只有宽度）。</summary>
    Texture1D = 1,

    /// <summary>二维纹理（宽 × 高），最常用。</summary>
    Texture2D = 2,

    /// <summary>三维纹理（宽 × 高 × 深，即体积纹理）。</summary>
    Texture3D = 3,

    /// <summary>一维纹理数组（多个切片，共享尺寸）。</summary>
    Texture1DArray = 4,

    /// <summary>二维纹理数组（常用于图集分层或立方体的面）。</summary>
    Texture2DArray = 5,

    /// <summary>立方体贴图（6 个面）。</summary>
    TextureCube = 6,

    /// <summary>立方体贴图数组。</summary>
    TextureCubeArray = 7,
}
