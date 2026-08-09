namespace Line.Framework.Graphics;

/// <summary>
/// 图形后端
/// </summary>
public enum GraphicBackend
{
    /// <summary>
    /// Metal:仅苹果支持
    /// </summary>
    Metal,

    /// <summary>
    /// Direct3D:仅Windows支持
    /// </summary>
    Direct3D,

    /// <summary>
    /// Vulkan:Windows Linux Android支持
    /// </summary>
    Vulkan,

    /// <summary>
    /// OpenGL:Windows Linux Android支持
    /// </summary>
    OpenGL,
}
