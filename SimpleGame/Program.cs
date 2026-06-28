using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI;
using Line.Framework.UI.DefaultWidget;
using Veldrid;

Log.SetMinLevel(LogLevel.Debug);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("Welcome to -Line-Framework");
var assembly = Assembly.GetExecutingAssembly();

var names = assembly.GetManifestResourceNames();
foreach (var name in names)
    Log.Debug($"Asset:{name}");

//新建窗口
BaseWindow a = new BaseWindow(Backend: GraphicsBackend.OpenGL);
a.FramePerSecond = 60;
a.UpdatePerSecond = 10000;

//音频
a.Audio.Create("MainTrack",assembly.GetManifestResourceStream("SimpleGame.assets.Example.mp3"));
var Audio=a.Resource.GetResource("MainTrack") as AudioResource;
var A=Audio.IsLoaded;
Console.WriteLine(A);
Audio.Play();
Audio.SetNaturalSpeed(1.2f);
var l=TAudio.Device.GetAllDevices();
for(int i = 0; i < l.Count; i++)
{
    Log.Debug($"{i}:{l[i].Name}");
}


Audio.Volume=0.3f;

//正方形底
var rect = new UIButton();
rect.parent = a.Root;
rect.Position = new(new(), new(0.5f, 0.5f));
rect.Size = new(new(350, 350), new());
rect.color = new(0, 42f / 255f, 125f / 255f, 1);
rect.Rotation = 0;
rect.anchor = new(0.5f, 0.5f);
rect.Z = 5;

//图标
var icon = new UIImage(a.Resource);
icon.parent = rect;
icon.Position = new(new(), new(0.5f, 0.5f));
icon.anchor = new(0.5f, 0.5f);
icon.Size = new(new(0, 0), new(1, 1));
icon.Color = new(1, 1, 1, 1);
var stream = assembly.GetManifestResourceStream("SimpleGame.assets.-L-F.png");
a.Resource.Create("Image", "SimpleGame.assets.-L-F.png", stream);
icon.TextureId = "SimpleGame.assets.-L-F.png";
icon.Z = 1;

//文字
var fs = assembly.GetManifestResourceStream("SimpleGame.assets.GenJyuuGothic-Normal-2.ttf");
a.Resource.Create("Font", "Font.SN Pro", fs);
var f = a.Resource.GetResource("Font.SN Pro") as Font;
f?.Size = 100;
var text = new UIText(a.Resource);
text.parent = a.Root;
text.Position = new(new(0, 200), new(0.5f, 0));
text.anchor = new(0.5f, 0.5f);
text.Text = "Welcome To -Line-Framework\nIt's just a simple Game Framework\n思源柔黑牛逼"; //Welcome To -Line-Framework\nIt's just a simple Game Framework
text.Size = new(new(500, 100), new());
text.Z = 20;
text.XAlignment = Alignment.Center;
text.YAlignment = Alignment.Right;
text.FontId = "Font.SN Pro";
text.FontScale = 1f;

//text.Text="Welcome";
text.Size = new(text.GetTextSize(text.Text) / new Vector2(1f, 1f), new());
text.color = new(1, 1, 1, 1);

//调试性息
var Per = new UIBox();
Per.parent = a.Root;
Per.color = new(1, 1, 1, 0.5f);
Per.anchor = new(1, 0);
Per.Position = new(new(-100, -100), new(1, 1));
Per.Size = new(new(150, 50), new());
Per.Z = 0;

//性息文本
var fs1 = assembly.GetManifestResourceStream("SimpleGame.assets.CascadiaMono.ttf");
a.Resource.Create("Font", "Font.CascadiaMono", fs1);
var f1 = a.Resource.GetResource("Font.CascadiaMono") as Font;
f1?.Size = 80;
var PerT = new UIText(a.Resource);
PerT.anchor = new(0, 0);
PerT.Position = new(new(), new(0, 0));
PerT.Size = new(new(), new(1, 1));
PerT.XAlignment = Alignment.Center;
PerT.YAlignment = Alignment.Right;
PerT.color = new(0, 1, 0, 1);
PerT.parent = Per;
PerT.Text = $"0FPS";
PerT.FontId = "Font.CascadiaMono";
PerT.FontScale = 0.5f;

double updD = 0;

//开转
Stopwatch sw = new();
sw.Start();
a.OnUpdate += (o, p) =>
{
    var r = sw.ElapsedMilliseconds / 1000f % 3f;
    r = r / 3f * 360f;
    rect.Rotation = r;
    icon.Rotation = r;
    updD = p.delay;
    if (a.TargetWindow.Focused)
    {
        a.FramePerSecond = 6000;
    }
    else
    {
        a.FramePerSecond = 10;
    }
};

a.OnUpdate += (o, p) =>
{
    updD=p.delay;
};

Stopwatch updateTime = new();
updateTime.Start();

double Fr = 0;

a.OnRender += (o, p) =>
{
    int f=(int)(1000/p.delay);
    Fr += (f - Fr) / 40;

    PerT.Text = $"{(uint)Fr} FPS";
    Per.Size = new(PerT.GetTextSize(PerT.Text) / new Vector2(1, 1), new());
};

/*
for (int i = 0; i < 20000; i++)
{
    var tmp = new UIBox();
    tmp.parent = a.Root;
}
*/
