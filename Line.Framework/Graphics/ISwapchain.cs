using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 交换链：呈现到窗口的那组后备缓冲。
/// <para>
/// <b>后端差异（调用方需要按约定写代码）</b>：
/// <list type="bullet">
/// <item><see cref="Resize"/>：Veldrid 必须与窗口像素尺寸保持同步，否则 present 失败
///       （表现为窗口永不更新）；SDL3 会自动跟随，是空操作。
///       因此<b>总是调用</b>即可。</item>
/// <item><see cref="Swap"/>：Veldrid 必需；SDL3 在提交命令时已自动呈现，是空操作。
///       同样<b>总是调用</b>即可。</item>
/// </list>
/// 两条都「无条件调用就能跨后端工作」，不需要判断后端。
/// </para>
/// <para>
/// 尺寸必须用像素尺寸（<c>SDL_GetWindowSizeInPixels</c>）而非逻辑尺寸，
/// 否则 HiDPI / 分数缩放下会因尺寸不匹配而静默呈现失败。
/// </para>
/// </summary>
public interface ISwapchain : IDisposable, IName
{
    /// <summary>
    /// 通知交换链窗口尺寸变化（像素尺寸）。
    /// SDL3 下是空操作；Veldrid 下必需。
    /// </summary>
    void Resize(int width, int height);

    /// <summary>本交换链的渲染目标，用作命令列表的 <c>SetFrameBuffer</c> 参数。</summary>
    IFrameBuffer FrameBuffer { get; }

    /// <summary>呈现当前帧。SDL3 下是空操作（提交即呈现）。</summary>
    void Swap();
}
