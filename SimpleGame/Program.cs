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
a.UpdatePerSecond = 120;
var audio = new AudioManager();
var b = new UIBox();
b.parent = a.Root;
b.Position=new(new(),new(0.5f,0.5f));
b.Size=new(new(100,100),new(0,0));
b.color = new(new(255f/255f, 255f/255f, 255f/255f, 0.8f));
b.anchor = new(0.5f,0.5f);
b.z=1;
b.name="box";
var c=new UIBox();
c.parent=b;
c.Position=new(new(0,0),new(0f,0f));
c.Size=new(new(0,0),new(1,1));
c.color = new(new(0f/255f, 255f/255f, 255f/255f, 1f));
c.anchor = new(0.5f,0.5f);
c.z=-1;
while (a.TargetWindow.Exists)
{
    b.rotation = 90; Thread.Sleep(10);
}