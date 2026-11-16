namespace Line.Framework.Graphics;

/// <summary>
/// 像素格式。
/// <para>
/// 命名规则：<c>R/G/B/A</c> + 位宽 + 数值类型。
/// <c>UNorm</c> = 无符号归一化（整数存储、着色器里读作 0..1 浮点），
/// <c>SNorm</c> = 有符号归一化，<c>Float</c> = 真浮点，
/// <c>UInt/SInt</c> = 不归一化的整数。
/// </para>
/// <para>
/// 数值显式从 0 递增而非依赖默认值，便于跨后端对照排查错位。
/// 2D 渲染与 UI 图集通常用 <see cref="R8G8B8A8UNorm"/>（RGBA 字节序）
/// 或 <see cref="B8G8R8A8UNorm"/>（BMP/DIB 等 BGRA 数据源）。
/// </para>
/// </summary>
public enum PixelFormat : ushort
{
    /// <summary>8 位单通道无符号归一化。</summary>
    R8UNorm = 0,

    /// <summary>8 位双通道无符号归一化。</summary>
    R8G8UNorm = 1,

    /// <summary>8 位 RGBA 无符号归一化（最常用的纹理格式）。</summary>
    R8G8B8A8UNorm = 2,

    /// <summary>8 位 BGRA 无符号归一化（Windows 位图 / 部分图像库的默认序）。</summary>
    B8G8R8A8UNorm = 3,

    /// <summary>16 位单通道无符号归一化。</summary>
    R16UNorm = 4,

    /// <summary>16 位双通道无符号归一化。</summary>
    R16G16UNorm = 5,

    /// <summary>16 位 RGBA 无符号归一化。</summary>
    R16G16B16A16UNorm = 6,

    /// <summary>8 位单通道有符号归一化。</summary>
    R8SNorm = 7,

    /// <summary>8 位双通道有符号归一化。</summary>
    R8G8SNorm = 8,

    /// <summary>8 位 RGBA 有符号归一化。</summary>
    R8G8B8A8SNorm = 9,

    /// <summary>16 位单通道有符号归一化。</summary>
    R16SNorm = 10,

    /// <summary>16 位双通道有符号归一化。</summary>
    R16G16SNorm = 11,

    /// <summary>16 位 RGBA 有符号归一化。</summary>
    R16G16B16A16SNorm = 12,

    /// <summary>16 位单通道浮点。</summary>
    R16Float = 13,

    /// <summary>16 位双通道浮点。</summary>
    R16G16Float = 14,

    /// <summary>16 位 RGBA 浮点。</summary>
    R16G16B16A16Float = 15,

    /// <summary>32 位单通道浮点。</summary>
    R32Float = 16,

    /// <summary>32 位双通道浮点。</summary>
    R32G32Float = 17,

    /// <summary>32 位 RGBA 浮点（HDR 场景的常用格式）。</summary>
    R32G32B32A32Float = 18,

    /// <summary>8 位单通道无符号整数（不归一化）。</summary>
    R8UInt = 19,

    /// <summary>8 位双通道无符号整数。</summary>
    R8G8UInt = 20,

    /// <summary>8 位 RGBA 无符号整数。</summary>
    R8G8B8A8UInt = 21,

    /// <summary>8 位单通道有符号整数。</summary>
    R8SInt = 22,

    /// <summary>8 位双通道有符号整数。</summary>
    R8G8SInt = 23,

    /// <summary>8 位 RGBA 有符号整数。</summary>
    R8G8B8A8SInt = 24,
}
