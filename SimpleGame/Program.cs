using System;
using System.Diagnostics;
using System.Dynamic;
using Line.Framework;
using Line.Framework.Graphics;
using Microsoft.VisualBasic.FileIO;
using SharpGen.Runtime.Win32;

Log.SetMinLevel(LogLevel.Info);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("日志系统启动完成");
Window a = new Window();
a.UpdatePerSecond = 800;
