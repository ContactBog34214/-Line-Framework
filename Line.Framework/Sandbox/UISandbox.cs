using System.Text.Json.Serialization;
using Line.Framework.IO;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.Sandbox;

public class UISandbox : UIWidget
{
    [JsonIgnore]
    /// <summary>
    /// 沙盒内部空间
    /// </summary>
    public InsideSandbox Sandbox { get; }
    [JsonIgnore]
    protected virtual InputManager MainIM { get; set; }
    [JsonIgnore]
    protected virtual UIScreen UIRoot { get; set; }
    public UISandbox()
    {
        Sandbox = new(this);
        IM = new(this);

        UIRoot = FindRoot() as UIScreen;
        MainIM = UIRoot?.InputManager;
        IM.im = MainIM;
        Sandbox.Scr = UIRoot;
    }
    public override void SetParent(UINode value)
    {
        base.SetParent(value);
        UIRoot = FindRoot() as UIScreen;
        MainIM = UIRoot?.InputManager;
        IM.im = MainIM;
        Sandbox.Scr = UIRoot;
    }
    [JsonIgnore]
    public bool Focus => FindWidgetPointTouched(UIRoot, MainIM?.CursorPosition ?? new()) == this;
    /// <summary>
    /// 在丢失所有引用后自动Dispose
    /// </summary>
    public bool AutoDispose { get; set; } = true;
    protected internal bool AllowDispose = false;
    protected readonly internal VirtualIM IM;
    /// <summary>
    /// 允许剪切板粘贴
    /// </summary>
    public DynamicValue<bool> AllowClipboardPaste { get; set; } = true;
    /// <summary>
    /// 允许获取输入状态
    /// </summary>
    public DynamicValue<bool> AllowGetTextInputState { get; set; } = true;
    /// <summary>
    /// 允许设置输入状态
    /// </summary>
    public DynamicValue<bool> AllowSetTextInputState { get; set; } = true;
    /// <summary>
    /// 允许获取输入内容
    /// </summary>
    public DynamicValue<bool> AllowGetTextInputContext { get; set; } = true;
    /// <summary>
    /// 允许获取鼠标位置
    /// </summary>
    public DynamicValue<bool> AllowGetMousePosition { get; set; } = true;
    /// <summary>
    /// 启用全局输入：即使光标不在沙盒内也可以输入
    /// </summary>
    public DynamicValue<bool> AllowGboalInput { get; set; } = true;
    /// <summary>
    /// 鼠标键黑名单
    /// </summary>
    public DynamicValue<MouseButton[]> MouseButtonBlackList { get; set; } =
        new MouseButton[] { };
    /// <summary>
    /// 鼠标键白名单模式
    /// </summary>
    public DynamicValue<bool> MouseButtonWhiteListMode { get; set; } = false;
    /// <summary>
    /// 允许获取鼠标滚轮数据
    /// </summary>
    public DynamicValue<bool> AllowGetMouseWheel { get; set; } = true;
    /// <summary>
    /// 键盘按键黑名单
    /// </summary>
    public DynamicValue<KeyCode[]> KeyBlackList { get; set; } = new KeyCode[] { };
    /// <summary>
    /// 键盘按键白名单模式
    /// </summary>
    public DynamicValue<bool> KeyWhiteListMode { get; set; } = false;
    /// <summary>
    /// 允许触摸
    /// </summary>
    public DynamicValue<bool> AllowTouch { get; set; } = true;
    public override void Dispose()
    {
        AllowDispose = true;
        Sandbox?.Dispose();
        IM?.Dispose();
        base.Dispose();
    }
    protected virtual SandCompositor Compositor { get; } = new();
    public override async Task RendererContext(RendererContextArgs args)
    {
        Sandbox.Parent = null;
        var collector = args.Collector;
        Position = new Coord2(new(), new());
        Sandbox.UpdateScreenSize((int)args.width, (int)args.height);
        Opacity = 1;
        collector.DrawVertex(await Compositor.Composite(Sandbox), this);
    }
    ~UISandbox()
    {
        if (AutoDispose)
            Dispose();
        GC.SuppressFinalize(this);
    }
}