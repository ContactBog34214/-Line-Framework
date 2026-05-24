using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Audio;
using Veldrid;
using Line.Framework.UI.DefaultWidget;
using System.Numerics;
using Line.Framework.UI;

Log.SetMinLevel(LogLevel.Info);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("日志系统启动完成");

//新建窗口
BaseWindow a = new BaseWindow(Backend: GraphicsBackend.OpenGLES);
a.FramePerSecond = 60;
a.UpdatePerSecond=60;

//一个盒子
var b = new UIBox();
b.parent = a.Root;
b.Position = new(new(), new(0,0));
b.Size = new(new(10, 10), new(0, 0));
b.color = new(new(255f / 255f, 255f / 255f, 255f / 255f, 0.8f));
b.anchor = new(0f, 0f);
b.Z = 1;
b.name = "box";

//一个按钮
var c = new UIButton();
c.parent=a.Root;
c.Position = new(new(), new(0.5f,0));
c.Size = new(new(100, 100), new(0, 0));
c.color = new(new(0f / 255f, 255f / 255f, 255f / 255f, 0.8f));
c.anchor = new(0.5f, 0.5f);
c.Z = 2;
c.UpdateRoot();

//一个指示鼠标的盒子
var d=new UIBox();
d.parent=a.Root;
d.Position=new(new(), new(0,0));
d.Size = new(new(10, 10), new(0, 0));
d.color = new(new(255f / 255f, 255f / 255f, 0f / 255f, 0.8f));
d.Z=1;

//绑定事件
a.Input.MouseMove += (dx,dy) =>
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
    var pos=c.Position.offset;
    pos.Y=a.Input.TotalMouseWheelDelta;
    c.Position=new(pos,c.Position.scale);
};

c.WhenClick += (o, p) =>
{
    Log.Info("Click");
};

a.Input.MouseMove += (x, y) =>
{
    d.Position=new(a.Input.TotalMouseDelta,d.Position.scale);
};

//主循环
a.OnUpdate+=(o,p)=>
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