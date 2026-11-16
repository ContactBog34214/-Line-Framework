namespace Line.Framework.Graphics
{
    /// <summary>
    /// 交换链的原生窗口来源。
    /// <para>
    /// <b>这是唯一必须按后端区分的数据</b>：SDL3 只把 <see cref="Surface"/>
    /// 当作 <c>SDL_Window*</c> 使用，忽略 <see cref="Kind"/> 与
    /// <see cref="Connection"/>；Veldrid 则严格按 <see cref="Kind"/>
    /// 解释这两个指针，用错必然无法呈现。
    /// </para>
    /// <para>
    /// Veldrid 的正确组合：
    /// X11 → Surface = Window 号, Connection = Display*；
    /// Wayland → Surface = wl_surface*, Connection = wl_display*；
    /// Win32 → Surface = HWND, Connection = HINSTANCE。
    /// </para>
    /// </summary>
    public struct SwapchainSource
    {
        /// <summary>主窗口句柄（含义由 <see cref="Kind"/> 决定）。</summary>
        public nint Surface { get; set; }

        /// <summary>显示连接句柄；X11 为 Display*，Wayland 为 wl_display*。</summary>
        public nint Connection { get; set; }

        /// <summary>窗口来源类型，决定上面两个指针如何解释。</summary>
        public NativeWindowKind Kind { get; set; }
    }

    /// <summary>
    /// 原生窗口类型。
    /// </summary>
    public enum NativeWindowKind
    {
        /// <summary>Windows：HWND。</summary>
        Win32,

        /// <summary>X11：Window 号 + Display*。</summary>
        Xlib,

        /// <summary>Wayland：wl_surface* + wl_display*。</summary>
        Wayland,

        /// <summary>macOS：NSWindow。</summary>
        Cocoa,

        /// <summary>Android：ANativeWindow。</summary>
        AndroidSurface,

        /// <summary>macOS 的 NSView（Metal 层）。</summary>
        MetalView,
    }
}
