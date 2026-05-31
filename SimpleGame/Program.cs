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
a.UpdatePerSecond = 1000;
a.TargetWindow.CursorVisible = false;

//一个盒子
var b = new UIBox();
b.parent = a.Root;
b.Position = new(new(), new(0.5f, 0.5f));
b.Size = new(new(100, 100), new(0, 0));
b.color = new(new(255f / 255f, 255f / 255f, 255f / 255f, 1f));
b.anchor = new(0f, 0f);
b.Z = 1;
b.name = "box";

//可视化位置处理
var h = new UIBox();
h.parent = a.Root;
h.Position = new(new(), new(0.5f, 0.5f));
h.Size = new(new(2, 2), new(0, 0));
h.color = new(new(0f / 255f, 255f / 255f, 0f / 255f, 1f));
h.anchor = new(0.5f, 0.5f);
h.Z = 1.5f;
h.name = "box";

//一个按钮
var c = new UIButton();
c.parent = a.Root;
c.Position = new(new(), new(0.5f, 0));
c.Size = new(new(100, 100), new(0, 0));
c.color = new(new(0f / 255f, 255f / 255f, 255f / 255f, 1f));
c.anchor = new(0.5f, 0.5f);
c.Z = 0;
c.rotation = 290;
c.UpdateRoot();

//一个指示鼠标的图片框
var d = new UIImage();
d.parent = b;
d.Position = new(new(), new(0, 0));
d.Size = new(new(75, 75), new(0, 0));
d.Color = new(new(255f / 255f, 255f / 255f, 255f / 255f, 1f));
d.Z = 10;
d.anchor = new(0.5f, 0.5f);
d.LoadImage(a.Dev, a.RendererClass.TextureLayout, "./assets/lazer.png");
d.visible = !a.TargetWindow.CursorVisible;

//特效列表
List<UIImage> l = [];

//绑定事件
a.Input.MouseMove += (dx, dy) => {
    /*
    var pos = b.Position.offset;
    pos.X += dx;
    pos.Y += dy;
    */
    //b.Position = new Coord2(c.GetPositionOnScreen(), b.Position.scale);
};

a.TargetWindow.MouseWheel += (n) =>
{
    var pos = c.Position.offset;
    pos.Y = a.Input.TotalMouseWheelDelta * 10;
    c.Position = new(pos, c.Position.scale);
};

c.WhenClick += (o, p) =>
{
    Log.Debug("Click");
};

//判断
a.OnUpdate += (o, p) =>
{
    if (c.HitTest(a.Input.TotalMouseDelta))
    {
        c.color = new(new(0f / 255f, 255f / 255f, 0f / 255f, 1f));
    }
    else
    {
        c.color = new(new(0f / 255f, 0f / 255f, 255f / 255f, 1f));
    }
    d.Position = new(a.Input.TotalMouseDelta, d.Position.scale);
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
            Z = -1,
            parent = d,
            BackgroundColor = d.BackgroundColor,
            rotation = d.rotation,
            Color = d.Color,
        }
    );
    l[l.Count - 1].LoadTexture(a.Dev, a.RendererClass.TextureLayout, d.Texture);
};

a.OnRender += (o, p) =>
{
    d.rotation += 1;
    c.rotation += 0.5f;
    h.Position = new(c.MousePosition(a.Input.TotalMouseDelta), h.Position.scale);
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

var u = 0;

for (int i = 0; i < 10; i++)
{
    var t = new UIBox();
    u++;
    t.parent = a.Root;
    t.Size=new(new(1,1),new());
}

a.OnUpdate += (o, p) =>
{
    //Console.WriteLine($"Update Delay:{p.delay}ms,{u} objects");
};

a.OnRender += (o, p) =>
{
    //Console.WriteLine($"Renderer Delay:{p.delay}ms,{u} objects");
};
