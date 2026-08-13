using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.UI;

public class UIScreen : UIWidget
{
    private int _screenWidth;

    /// <summary>
    /// 窗口
    /// </summary>
    protected virtual WindowType window { get; init; }
    /// <summary>
    /// 输入器
    /// </summary>
    public virtual InputManager InputManager { get => window.Input; }
    /// <summary>
    /// 当窗口更新时
    /// </summary>
    public virtual event Action<double> OnRender;
    /// <summary>
    /// 当窗口更新时
    /// </summary>
    public virtual event Action<double> OnUpdate;
    /// <summary>
    /// 设置文本输入启用状态
    /// </summary>
    public virtual bool TextInput
    {
        get => window != null ? window.TextInput : Screen.TextInput;
        set
        {
            if (window != null) window.TextInput = value; else Screen.TextInput = value;
        }
    }
    protected int _screenHeight;
    protected UIScreen Screen;

    public UIScreen(WindowType w, int screenWidth, int screenHeight)
    {
        OR = a => OnRender?.Invoke(a);
        OU = a => OnUpdate?.Invoke(a);
        window = w;
        // 固定位置为 (0,0)
        Position = new Coord2 { scale = Vector2.Zero, offset = Vector2.Zero };
        // 设置大小为屏幕像素尺寸
        UpdateScreenSize(screenWidth, screenHeight);
        // 锚点设为左上角 (0,0)
        Anchor = Vector2.Zero;
        Visible = true;
        Index = 0;
        s = new(() =>
        {
            return new(_screenWidth, _screenHeight);
        });
        p = new Vector2(0, 0);
        o = 1;
        oz = 0;
        w?.OnRender += OR;
        w?.OnUpdate += OU;
    }
    public UIScreen(UIScreen w, int screenWidth, int screenHeight)
    {
        OR = a => OnRender?.Invoke(a);
        OU = a => OnUpdate?.Invoke(a);
        Screen = w;
        // 固定位置为 (0,0)
        Position = new Coord2 { scale = Vector2.Zero, offset = Vector2.Zero };
        // 设置大小为屏幕像素尺寸
        UpdateScreenSize(screenWidth, screenHeight);
        // 锚点设为左上角 (0,0)
        Anchor = Vector2.Zero;
        Visible = true;
        Index = 0;
        s = new(() =>
        {
            return new(_screenWidth, _screenHeight);
        });
        p = new Vector2(0, 0);
        o = 1;
        oz = 0;
        w?.OnRender += OR;
        w?.OnUpdate += OU;
    }
    protected Action<double> OR;
    protected Action<double> OU;
    /// <summary>
    /// 更新屏幕大小
    /// </summary>
    /// <param name="宽"></param>
    /// <param name="高"></param>
    public void UpdateScreenSize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;
        // 将 Size 设置为绝对像素值（scale=0, offset=宽高）
        Size = new Coord2
        {
            scale = Vector2.Zero,
            offset = new Vector2(width, height) * (window?.Scale ?? 1),
        };
    }

    public override bool HitTest(Vector2 mousePixel)
    {
        return Visible;
    }

    // 可选：提供屏幕尺寸属性供子控件进行百分比布局计算
    /// <summary>
    /// 屏幕宽
    /// </summary>
    public int ScreenWidth => _screenWidth;

    /// <summary>
    /// 屏幕高
    /// </summary>
    public int ScreenHeight => _screenHeight;
    public override void Dispose()
    {
        base.Dispose();
        window?.OnRender -= OR;
        window?.OnUpdate -= OU;
        Screen?.OnRender -= OR;
        Screen?.OnUpdate -= OU;
    }
}
