namespace Line.Framework.Graphics
{
    /// <summary>
    /// 创建交换链的参数。
    /// </summary>
    public struct SwapchainCreateInfo
    {
        /// <summary>
        /// 原生窗口句柄的载体。语义随后端不同：
        /// SDL3 期望 <see cref="SwapchainSource.Surface"/> 直接是 <c>SDL_Window*</c>；
        /// Veldrid 期望按 <see cref="SwapchainSource.Kind"/> 给出对应的原生句柄。
        /// </summary>
        public SwapchainSource Source { get; set; }

        /// <summary>
        /// 宽度（<b>像素</b>尺寸，不是逻辑尺寸）。
        /// HiDPI 或分数缩放下两者不同，用错会让呈现静默失败。
        /// </summary>
        public uint Width { get; set; }

        /// <summary>高度（<b>像素</b>尺寸）。</summary>
        public uint Height { get; set; }

        /// <summary>
        /// 深度格式；null 表示交换链不带深度缓冲。
        /// SDL3 后端忽略此项（始终没有深度附件）。
        /// </summary>
        public PixelFormat? DepthFormat { get; set; }

        /// <summary>
        /// 是否垂直同步。帧率稳定性优先时设为 true；
        /// 需要更低输入延迟时设为 false（本框架的 demo 用 false）。
        /// </summary>
        public bool SyncToVerticalBlank { get; set; }

        /// <summary>是否使用 sRGB 颜色空间的后备缓冲。</summary>
        public bool ColorSrgb { get; set; }
    }
}
