using Line.Framework.Graphics;
using Line.Framework.IO;
using S2 = SDL2.SDL;

namespace Line.Framework.Default.IO;

/// <summary>
/// 抽象层 <see cref="WindowFlags"/> → SDL2 窗口标志。
/// <para>
/// <b>SDL2-CS 的标志是枚举 <c>SDL_WindowFlags</c> 的成员</b>，不是
/// <c>SDL</c> 类上的常量 —— 写成 <c>SDL.SDL_WINDOW_HIDDEN</c> 会编译失败。
/// </para>
/// </summary>
internal static class SDL2Converter
{
    public static S2.SDL_WindowFlags ConvertWindowFlags(WindowFlags flags)
    {
        S2.SDL_WindowFlags result = 0;

        // SDL2 默认就是 SHOWN；只有显式要 Hidden 才设。
        if ((flags & WindowFlags.Hidden) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_HIDDEN;

        if ((flags & WindowFlags.Resizable) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_RESIZABLE;

        if ((flags & WindowFlags.Borderless) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_BORDERLESS;

        if ((flags & WindowFlags.AlwaysOnTop) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;

        // 语义压扁：抽象层只有一个 bool，这里选「无边框全屏」。
        // 独占全屏（SDL_WINDOW_FULLSCREEN）会改显示模式，副作用更大。
        if ((flags & WindowFlags.Fullscreen) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;

        // 不设这个标志时 SDL2 不保证后备缓冲跟 DPI 走。
        if ((flags & WindowFlags.HighPixelDensity) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI;

        if ((flags & WindowFlags.InputFocus) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS;

        if ((flags & WindowFlags.MouseFocus) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS;

        // SDL2 没有独立的透明位，用无边框近似。
        if ((flags & WindowFlags.Transparent) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_BORDERLESS;

        if ((flags & WindowFlags.Utility) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_UTILITY;

        if ((flags & WindowFlags.Tooltip) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_TOOLTIP;

        if ((flags & WindowFlags.PopupMenu) != 0)
            result |= S2.SDL_WindowFlags.SDL_WINDOW_POPUP_MENU;

        return result;
    }

    /// <summary>
    /// 抽象层像素格式 → SDL2 surface 的通道掩码。
    /// <para>
    /// <c>SDL_CreateRGBSurfaceFrom</c> 收的是 4 个掩码而不是格式枚举。
    /// 掩码按<b>小端下的 32 位打包值</b>给出，与
    /// <c>SDL3Converter.ConvertSurfacePixelFormat</c> 用 ABGR/ARGB 是同一个道理。
    /// </para>
    /// </summary>
    public static (uint r, uint g, uint b, uint a) ConvertSurfaceMasks(PixelFormat format)
    {
        return format switch
        {
            // 内存字节序 R,G,B,A → 小端 32 位读作 0xAABBGGRR
            PixelFormat.R8G8B8A8UNorm => (0x000000FFu, 0x0000FF00u, 0x00FF0000u, 0xFF000000u),

            // 内存字节序 B,G,R,A → 小端 32 位读作 0xAARRGGBB
            PixelFormat.B8G8R8A8UNorm => (0x00FF0000u, 0x0000FF00u, 0x000000FFu, 0xFF000000u),

            _ => throw new NotSupportedException(
                $"PixelFormat {format} has no SDL2 surface equivalent. "
                    + "Only 8-bit RGBA/BGRA layouts are supported for surfaces."
            ),
        };
    }
}
