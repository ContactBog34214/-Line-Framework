using System.Numerics;

namespace Line.Framework.Graphics;

/// <summary>
/// 全屏模式参数（供窗口 / 显示切换使用）。
/// <para>
/// 只是一组描述值，不做任何校验或平台查询 —— 实际能否进入该模式
/// 取决于视频驱动。抽象层目前没有暴露切换全屏的入口。
/// </para>
/// </summary>
public struct FullScreenMode
{
    /// <summary>分辨率（像素）。</summary>
    public Vector2 Size { get; init; }

    /// <summary>刷新率（Hz）。</summary>
    public float RefreshRate { get; init; }

    /// <summary>像素密度（DPI 缩放因子），用于分数缩放下的尺寸换算。</summary>
    public float PixelDensity { get; init; }

    /// <summary>构造一组全屏参数。</summary>
    /// <param name="s">分辨率（像素）。</param>
    /// <param name="rfr">刷新率（Hz）。</param>
    /// <param name="pd">像素密度。</param>
    public FullScreenMode(Vector2 s, float rfr, float pd)
    {
        Size = s;
        RefreshRate = rfr;
        PixelDensity = pd;
    }
}
