using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Audio;
using Veldrid;
using Line.Framework.UI.DefaultWidget;
using System.Numerics;

Log.SetMinLevel(LogLevel.Info);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("日志系统启动完成");
BaseWindow a = new BaseWindow(Backend: GraphicsBackend.OpenGL);
a.FramePerSecond = 1000;
a.UpdatePerSecond=1000;
var audio = new AudioManager();
var b = new UIBox();
b.parent = a.Root;
b.Position = new(new(), new(0,0));
b.Size = new(new(100, 100), new(0, 0));
b.color = new(new(255f / 255f, 255f / 255f, 255f / 255f, 0.8f));
b.anchor = new(0.5f, 0.5f);
b.z = 1;
b.name = "box";
var c = new UIBox();
c.parent=a.Root;
c.Position = new(new(), new(0,0));
c.Size = new(new(100, 100), new(0, 0));
c.color = new(new(0f / 255f, 255f / 255f, 255f / 255f, 0.8f));
c.anchor = new(0.5f, 0.5f);
c.z = 2;
a.Input.MouseMove += (dx,dy) =>
{
    var pos = b.Position.offset;
    pos.X += dx;
    pos.Y += dy;
    b.Position = new Coord2(pos, b.Position.scale);
};
a.TargetWindow.MouseWheel += (n) =>
{
    var pos=c.Position.offset;
    pos.Y=a.Input.TotalMouseWheelDelta;
    c.Position=new(pos,c.Position.scale);
};