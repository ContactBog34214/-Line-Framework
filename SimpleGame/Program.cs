using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Audio;
using Veldrid;

Log.SetMinLevel(LogLevel.Info);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("日志系统启动完成");
BaseWindow a = new BaseWindow(Backend : GraphicsBackend.OpenGL);
a.UpdatePerSecond = 800;
var audio=new AudioManager();
