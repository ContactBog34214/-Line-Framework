using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Types;
using Line.Framework.UI;

namespace Line.Framework.UI;

public class UIScreen : UIWidget
{
    private int _screenWidth;
    public WindowType window { get; init; }
    private int _screenHeight;

    public UIScreen(WindowType w, int screenWidth, int screenHeight)
    {
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
    }

    public void UpdateScreenSize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;
        // 将 Size 设置为绝对像素值（scale=0, offset=宽高）
        Size = new Coord2
        {
            scale = Vector2.Zero,
            offset = new Vector2(width, height) * window.Scale,
        };
    }

    public override bool HitTest(Vector2 mousePixel)
    {
        return Visible;
    }

    // 可选：提供屏幕尺寸属性供子控件进行百分比布局计算
    public int ScreenWidth => _screenWidth;
    public int ScreenHeight => _screenHeight;
}
