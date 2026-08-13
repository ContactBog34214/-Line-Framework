using System.Numerics;
using Line.Framework.Types;

namespace Line.Framework.UI;

public abstract class UIWidget : UINode
{
    /// <summary>
    /// 位置
    /// </summary>
    public DynamicValue<Coord2> Position { get; set; } = new(new Coord2());

    /// <summary>
    /// 大小
    /// </summary>
    public DynamicValue<Coord2> Size { get; set; } = new(new Coord2());

    /// <summary>
    /// 锚点(控件中心点)
    /// </summary>
    public DynamicValue<Vector2> Anchor { get; set; } = new(new Vector2(0, 0));

    /// <summary>
    /// 子控件偏移
    /// </summary>
    public DynamicValue<Coord2> ChildrenOffset { get; set; } = new Coord2();

    /// <summary>
    /// 是否可见
    /// </summary>
    public DynamicValue<bool> Visible { get; set; } = true;

    /// <summary>
    /// 获取在屏幕上的绝对位置
    /// </summary>
    /// <returns>绝对位置</returns>
    public Vector2 GetPositionOnScreen()
    {
        Vector2 si = new(0, 0);
        Coord2 of = new();
        UIWidget pa = null;
        if (Parent != null && Parent is UIWidget i)
        {
            i = Parent as UIWidget;
            si = (i?.GetSizeOnScreen()??new()) * (i?.Anchor??new());
            pa = i;
            of = i?.ChildrenOffset??new();
        }
        return new(
            s.Value.X * (Position.Value.scale.X + of.scale.X)
                + of.offset.X
                + Position.Value.offset.X
                - GetSizeOnScreen().X * Anchor.Value.X
                + p.Value.X
                - si.X,
            s.Value.Y * (Position.Value.scale.Y + of.scale.Y)
                + of.offset.Y
                + Position.Value.offset.Y
                - GetSizeOnScreen().Y * Anchor.Value.Y
                + p.Value.Y
                - si.Y
        );
    }

    /// <summary>
    /// 获取在屏幕上的绝对大小
    /// </summary>
    /// <returns>绝对大小</returns>
    public Vector2 GetSizeOnScreen()
    {
        return new(
            s.Value.X * Size.Value.scale.X + Size.Value.offset.X,
            s.Value.Y * Size.Value.scale.Y + Size.Value.offset.Y
        );
    }

    public virtual async Task RendererContext(RendererContextArgs args) { }

    protected UIWidget()
    {
        s = new(() =>
        {
            if (Parent is UIWidget t)
                return new(
                    t.Size.Value.offset.X + t.Size.Value.scale.X * t.s.Value.X,
                    t.Size.Value.offset.Y + t.Size.Value.scale.Y * t.s.Value.Y
                );
            return new Vector2(0);
        });
        o = new(() =>
        {
            if (Parent is UIWidget a)
                return a.o * Opacity;
            return Opacity;
        });
        p = new(() =>
        {
            if (Parent is UIWidget t)
                return new(
                    t.Position.Value.offset.X
                        + t.Position.Value.scale.X * t.s.Value.X
                        + t.p.Value.X,
                    t.Position.Value.offset.Y + t.Position.Value.scale.Y * t.s.Value.Y + t.p.Value.Y
                );
            return new Vector2(0, 0);
        });
    }

    internal float oz = 0;
    internal DynamicValue<Vector2> s { get; set; }
    public TouchModes TouchMode { get; set; } = TouchModes.All;
    internal DynamicValue<Vector2> p { get; set; }
    internal bool syncOK = false;
    internal DynamicValue<float> o { get; set; }
    internal List<Vector2[]> ClipList = [];

    /// <summary>
    /// 旋转角度
    /// </summary>
    public DynamicValue<float> Rotation { get; set; } = 0;

    /// <summary>
    /// 不透明度
    /// </summary>
    public DynamicValue<float> Opacity { get; set; } = 1;

    /// <summary>
    /// 获取与鼠标的相对坐标
    /// </summary>
    /// <param name="鼠标绝对坐标"></param>
    /// <returns>相对坐标</returns>
    public Vector2 MousePosition(Vector2 mousePixel)
    {
        var P = GetPositionOnScreen();
        var s = GetSizeOnScreen();
        //到相对
        var tmp = mousePixel - P - Anchor * s;

        //旋转
        double r = (double)Rotation % 360d;
        r = -r;
        double cos = Math.Cos(r * Math.PI / 180f);
        double sin = Math.Sin(r * Math.PI / 180f);
        tmp = new((float)(tmp.X * cos - tmp.Y * sin), (float)(tmp.Y * cos + tmp.X * sin));

        return tmp + Anchor * s;
    }

    /// <summary>
    /// 判断是否碰到
    /// </summary>
    /// <param name="绝对位置"></param>
    /// <returns>是否碰对</returns>
    public virtual bool HitTest(Vector2 mousePixel)
    {
        var tmp = MousePosition(mousePixel);
        var S = GetSizeOnScreen();
        return 0 <= tmp.X && 0 <= tmp.Y && tmp.X <= S.X && tmp.Y <= S.Y;
    }

    /// <summary>
    /// 寻找被触碰的UI控件
    /// </summary>
    /// <param name="根UI"></param>
    /// <param name="绝对坐标"></param>
    /// <returns>被碰到的UI控件</returns>
    public static UIWidget FindWidgetPointTouched(UIWidget w, Vector2 Point)
    {
        UIWidget[] Children = w.Children.OfType<UIWidget>().OrderBy(c => c.Index).ToArray();
        for (int i = Children.Length; ; )
        {
            i--;
            if (i < 0)
                break;
            if (Children[i].HitTest(Point))
            {
                switch (Children[i].TouchMode)
                {
                    case (TouchModes.All):
                        return FindWidgetPointTouched(Children[i], Point);
                    case (TouchModes.Children):
                        var t = FindWidgetPointTouched(Children[i], Point);
                        if (t != null && t != Children[i])
                            return t;
                        break;
                }
            }
        }
        return w;
    }

    /// <summary>
    /// 判断UI控件是否被无遮挡地碰到
    /// </summary>
    /// <param name="根UI"></param>
    /// <param name="目标UI控件"></param>
    /// <param name="绝对坐标"></param>
    /// <returns>是否无遮挡碰到</returns>
    public static bool IsWidgetPointTouched(UIWidget w, UIWidget t, Vector2 Point)
    {
        UIWidget[] Children = w.Children.OfType<UIWidget>().OrderBy(c => c.Index).ToArray();
        for (int i = Children.Length; ; )
        {
            i--;
            if (i < 0)
                break;
            if (Children[i].HitTest(Point))
            {
                switch (Children[i].TouchMode)
                {
                    case (TouchModes.All):
                        if (Children[i] == t)
                            return true;
                        return IsWidgetPointTouched(Children[i], t, Point);
                    case (TouchModes.Children):
                        if (Children[i] == t)
                            return false;
                        return IsWidgetPointTouched(Children[i], t, Point);
                }
            }
        }
        return false;
    }
}

public class RendererContextArgs
{
    public double X { get; set; }
    public double Y { get; set; }
    public double width { get; set; }
    public double height { get; set; }
    public UIDrawCollector Collector { get; set; }
}

public enum TouchModes
{
    /// <summary>
    /// 不可触碰
    /// </summary>
    None,

    /// <summary>
    /// 仅子UI控件
    /// </summary>
    Children,

    /// <summary>
    /// 子UI控件与本身
    /// </summary>
    All,
}
