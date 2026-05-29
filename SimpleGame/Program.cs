using System.Numerics;
using Line.Framework;
using Line.Framework.Audio;
using Line.Framework.Graphics;
using Line.Framework.UI;
using Line.Framework.UI.DefaultWidget;
using TagLib.Riff;
using Veldrid;

Log.SetMinLevel(LogLevel.Debug);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("日志系统启动完成");

//新建窗口
BaseWindow a = new BaseWindow(Backend: GraphicsBackend.OpenGL);
a.FramePerSecond = 60;
a.UpdatePerSecond = 120;
a.TargetWindow.CursorVisible = false;

//一个盒子
var b = new UIBox();
b.parent = a.Root;
b.Position = new(new(), new(0, 0));
b.Size = new(new(10, 10), new(0, 0));
b.color = new(new(255f / 255f, 255f / 255f, 255f / 255f, 0.8f));
b.anchor = new(0f, 0f);
b.Z = 1;
b.name = "box";

//一个按钮
var c = new UIButton();
c.parent = a.Root;
c.Position = new(new(), new(0.5f, 0));
c.Size = new(new(100, 100), new(0, 0));
c.color = new(new(0f / 255f, 255f / 255f, 255f / 255f, 0.8f));
c.anchor = new(0.5f, 0.5f);
c.Z = 2;
c.UpdateRoot();

//一个指示鼠标的图片框
var d = new UIImage();
d.parent = a.Root;
d.Position = new(new(), new(0, 0));
d.Size = new(new(75, 75), new(0, 0));
d.BackgroundColor = new(new(255f / 255f, 255f / 255f, 255f / 255f, 1f));
d.Z = 1000;
d.anchor = new(0.5f, 0.5f);
d.LoadImage(a.Dev, a.RendererClass.TextureLayout, "./assets/lazer.png");

//特效列表
List<UIImage> l = [];

//绑定事件
a.Input.MouseMove += (dx, dy) =>
{
    /*
    var pos = b.Position.offset;
    pos.X += dx;
    pos.Y += dy;
    */
    b.Position = new Coord2(c.GetPositionOnScreen(), b.Position.scale);
};

a.TargetWindow.MouseWheel += (n) =>
{
    var pos = c.Position.offset;
    pos.Y = a.Input.TotalMouseWheelDelta;
    c.Position = new(pos, c.Position.scale);
};

c.WhenClick += (o, p) =>
{
    Log.Debug("Click");
};

a.Input.MouseMove += (x, y) =>
{
    d.Position = new(a.Input.TotalMouseDelta, d.Position.scale);
};

//判断
a.OnUpdate += (o, p) =>
{
    if (UIWidget.HitTest(c.GetPositionOnScreen(), c.GetSizeOnScreen(), a.Input.TotalMouseDelta))
    {
        c.color = new(new(0f / 255f, 255f / 255f, 0f / 255f, 0.8f));
    }
    else
    {
        c.color = new(new(0f / 255f, 0f / 255f, 255f / 255f, 0.8f));
    }
};

a.Input.MouseDown += (o) =>
{
    l.Add(
        new()
        {
            Position = new(a.Input.TotalMouseDelta, new()),
            Size = new(d.Size.offset, new()),
            Opacity = 1,
            anchor = d.anchor,
            Z = 0,
            parent = d,
            BackgroundColor = new(new(255f / 255f, 255f / 255f, 255f / 255f, 1f)),
            rotation=d.rotation,
        }
    );
    l[l.Count - 1].LoadTexture(a.Dev, a.RendererClass.TextureLayout, d.Texture);
};

a.OnRender += (o, p) =>
{
    d.rotation+=1;
    List<UIImage> Deleting = [];
    for (int i = 0; i < l.Count; i++)
    {
        var t = l[i];
        t.Opacity += (0 - t.Opacity) / 30;
        var s = d.Size.offset;
        t.Size = new(new(s.X * (4 - 3 * t.Opacity), s.Y * (4 - 3 * t.Opacity)), new());
        if (t.Opacity < 0.02)
        {
            Deleting.Add(t);
        }
    }
    foreach (var a in Deleting)
    {
        a.Dispose();
        l.Remove(a);
    }
};
