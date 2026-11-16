using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 纹理：GPU 侧的像素数据容器。
/// <para>
/// 用途必须在创建时如实声明（见 <see cref="TextureUsage"/>），
/// 否则后续绑定时会被后端拒绝。
/// </para>
/// <para>
/// <see cref="Update"/> 支持局部更新，但每次调用都会产生一次上传 ——
/// 大纹理的首帧填充应尽量合并成一次调用。
/// </para>
/// </summary>
public interface ITexture : IDisposable, IName
{
    /// <summary>宽度（纹素）。</summary>
    uint Width { get; }

    /// <summary>高度（纹素）。一维纹理为 1。</summary>
    uint Height { get; }

    /// <summary>数组层数。非数组纹理为 1。</summary>
    uint ArrayLayers { get; }

    /// <summary>mip 层数。</summary>
    uint MipLevels { get; }

    /// <summary>深度（三维纹理用）。非三维纹理为 1。</summary>
    uint Depth { get; }

    /// <summary>维度与排布形式。</summary>
    TextureType Type { get; }

    /// <summary>像素格式。</summary>
    PixelFormat Format { get; }

    /// <summary>多重采样数。</summary>
    SampleCount SampleCount { get; }

    /// <summary>用途标志。</summary>
    TextureUsage Usage { get; }

    /// <summary>
    /// 上传像素数据到指定区域。
    /// </summary>
    /// <param name="source">
    /// 源数据；长度必须恰好等于该区域的字节数
    /// （由 <see cref="PixelFormat"/> 与区域尺寸算出），不匹配会抛异常。
    /// </param>
    /// <param name="x">目标区域左上角 X。</param>
    /// <param name="y">目标区域左上角 Y。</param>
    /// <param name="z">目标区域起始深度（三维纹理用）。</param>
    /// <param name="width">区域宽度；null 表示从 x 到纹理右边界。</param>
    /// <param name="height">区域高度；null 表示从 y 到下边界。</param>
    /// <param name="depth">区域深度；null 表示 1。</param>
    /// <param name="mipLevel">目标 mip 层。</param>
    /// <param name="arrayLayer">目标数组层。</param>
    void Update(
        ReadOnlySpan<byte> source,
        int x = 0,
        int y = 0,
        int z = 0,
        int? width = null,
        int? height = null,
        int? depth = null,
        uint mipLevel = 0,
        uint arrayLayer = 0
    );
}
