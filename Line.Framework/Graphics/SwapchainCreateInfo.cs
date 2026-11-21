using Line.Framework.IO;

namespace Line.Framework.Graphics
{
    /// <summary>
    /// 交换链的来源：<b>优先窗口，其次裸源</b>。
    /// <para>
    /// 两者都给时只看 <see cref="Window"/>，<see cref="Source"/> 被<b>完全忽略</b> ——
    /// 不合并、不比对、不兜底。合并会产生「一半句柄来自 A、一半来自 B」
    /// 这种无法调试的状态。
    /// </para>
    /// <para>
    /// 隐式转换让调用点可以写 <c>createInfo.Target = window;</c> 或
    /// <c>createInfo.Target = source;</c>；需要两者都给时用构造器。
    /// </para>
    /// </summary>
    public readonly struct SwapchainTarget
    {
        public SwapchainTarget(IWindow window, SwapchainSource source)
        {
            Window = window;
            Source = source;
        }

        /// <summary>窗口；非 null 时优先使用。</summary>
        public IWindow Window { get; }

        /// <summary>裸来源，用于「手上只有原生句柄」的高级用法。</summary>
        public SwapchainSource Source { get; }

        public bool HasWindow => Window is not null;

        /// <summary>
        /// 两者都空。
        /// <para>
        /// <b>不要用 <see cref="SwapchainSource.Kind"/> 判断有无句柄</b>：
        /// <c>default(SwapchainSource).Kind</c> 是 <c>Win32</c>（枚举从 0 开始），
        /// 一个裸 <c>default</c> 会被误判成「有个 HWND = 0 的 Win32 窗口」。
        /// 只能看 Surface / Connection 是否为 0。
        /// </para>
        /// </summary>
        public bool IsEmpty =>
            Window is null && Source.Surface == nint.Zero && Source.Connection == nint.Zero;

        /// <summary>
        /// 从窗口构造。
        /// <para>
        /// <b>为什么不是隐式转换</b>：C# 禁止「接口 → 结构体」的用户定义转换
        /// （CS0552），所以给窗口只能走显式工厂；裸 <see cref="SwapchainSource"/>
        /// 是结构体 → 结构体，可以直接用隐式转换。
        /// </para>
        /// </summary>
        public static SwapchainTarget FromWindow(IWindow window) => new(window, default);

        /// <summary>从裸源构造。</summary>
        public static SwapchainTarget FromSource(SwapchainSource source) => new(null, source);

        /// <summary>
        /// 裸源可以隐式转换（结构体 → 结构体）。
        /// 窗口请用 <see cref="FromWindow"/>：接口到结构体的隐式转换在 C# 里不合法。
        /// </summary>
        public static implicit operator SwapchainTarget(SwapchainSource source) =>
            new(null, source);
    }

    /// <summary>
    /// 创建交换链的参数。
    /// </summary>
    public struct SwapchainCreateInfo
    {
        /// <summary>
        /// 来源。优先 <see cref="SwapchainTarget.Window"/>。
        /// </summary>
        public SwapchainTarget Target { get; set; }

        /// <summary>
        /// 宽度。
        /// <para>
        /// <b>只有在 <see cref="Target"/> 没有窗口时才被使用</b> ——
        /// 有窗口时一律用 <see cref="IWindow.PixelSize"/> 覆盖，见 <see cref="Resolve"/>。
        /// </para>
        /// </summary>
        public uint Width { get; set; }

        /// <summary>高度，规则同 <see cref="Width"/>。</summary>
        public uint Height { get; set; }

        /// <summary>
        /// 用窗口作为来源（优先于 <see cref="Target"/> 里的裸源）。
        /// <para>
        /// 这只是 <c>Target = SwapchainTarget.FromWindow(value)</c> 的书写便利 ——
        /// 因为 C# 不允许接口 → 结构体的隐式转换。
        /// </para>
        /// </summary>
        public IWindow Window
        {
            set => Target = SwapchainTarget.FromWindow(value);
        }

        /// <summary>
        /// 深度格式；null 表示不带深度缓冲。
        /// </summary>
        public PixelFormat? DepthFormat { get; set; }

        /// <summary>
        /// 是否垂直同步。
        /// <para>
        /// 帧率稳定性优先时 true；要更低输入延迟时 false。
        /// 创建后仍可通过 <see cref="ISwapchain.SyncToVerticalBlank"/> 切换
        /// （注意该属性的粒度差异）。
        /// </para>
        /// </summary>
        public bool SyncToVerticalBlank { get; set; }

        /// <summary>是否使用 sRGB 颜色空间的后备缓冲。</summary>
        public bool ColorSrgb { get; set; }

        /// <summary>
        /// 解析出真正要用的原生来源与尺寸。两个后端共用，保证
        /// 「尺寸只有一个真相源」这条契约只有一份实现。
        /// </summary>
        /// <param name="source">解析后的原生来源。</param>
        /// <param name="width">像素宽度。</param>
        /// <param name="height">像素高度。</param>
        /// <exception cref="ArgumentException">
        /// 既没有窗口也没有裸源，或没有窗口时尺寸为 0。
        /// </exception>
        public void Resolve(out SwapchainSource source, out uint width, out uint height)
        {
            if (Target.HasWindow)
            {
                var window = Target.Window;
                source = window.Source;

                // 像素尺寸是唯一可用于交换链的尺寸；逻辑尺寸在 HiDPI / 分数缩放下
                // 与它不同，用错会让 present 静默失败（窗口开着、画面不动、不报错）。
                var pixel = window.PixelSize;
                width = (uint)MathF.Max(1f, pixel.X);
                height = (uint)MathF.Max(1f, pixel.Y);

                // 给裸源也以窗口为准，只提示、不改行为。
                if (Target.Source.Surface != nint.Zero || Target.Source.Connection != nint.Zero)
                    Log.Debug(
                        "SwapchainCreateInfo: Target.Window is set, Target.Source is ignored."
                    );

                return;
            }

            if (Target.IsEmpty)
                throw new ArgumentException(
                    "SwapchainCreateInfo.Target is empty: provide an IWindow or a SwapchainSource.",
                    nameof(Target)
                );

            source = Target.Source;
            width = Width;
            height = Height;

            if (width == 0 || height == 0)
                throw new ArgumentException(
                    $"Swapchain size must be non-zero when no window is given, "
                        + $"got {width}x{height}.",
                    nameof(Target)
                );
        }
    }
}
