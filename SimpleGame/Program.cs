using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Line.Framework;
using Line.Framework.Audio;
using Line.Framework.Graphics;
using Line.Framework.Resource.Graphic;
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
a.FramePerSecond = 10000;
a.UpdatePerSecond = 1000;

//初始化音频
var audio = new AudioManager();

//正方形底
var rect = new UIButton();
rect.parent = a.Root;
rect.Position = new(new(), new(0.5f, 0.5f));
rect.Size = new(new(350, 350), new());
rect.color = new(0, 42f / 255f, 125f / 255f, 1);
rect.rotation = 0;
rect.anchor = new(0.5f, 0.5f);
rect.Z = -1;

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
var fs = assembly.GetManifestResourceStream("SimpleGame.assets.Font.ttf");
a.Resource.Create("Font", "Font.SN Pro", fs);
var f = a.Resource.GetResource("Font.SN Pro") as Font;
f?.Size = 100;
var text = new UIText(a.Resource);
text.parent = a.Root;
text.Position = new(new(0, 200), new(0.5f, 0));
text.anchor = new(0.5f, 0.5f);
text.Text = "Welcome To -Line-Framework\nIt's just a simple Game Framework"; //Welcome To -Line-Framework\nIt's just a simple Game Framework
text.Size = new(new(500, 100), new());
text.Z = 20;
text.XAlignment = Alignment.Left;
text.YAlignment = Alignment.Right;
text.FontId = "Font.SN Pro";
text.FontScale = 1f;

//text.Text="Welcome";
text.Size = new(text.GetTextSize(text.Text) / new Vector2(1f, 0.8f), new());
text.color = new(1, 1, 1, 1);

//可视化区域
var textBox = new UIBox();
textBox.parent = text;
textBox.Position = new(new(0, 0), new(0, 0));
textBox.Size = new(new(), new(1, 1));
textBox.color = new(1, 1, 1, 0.2f);

//调试性息
var Per = new UIBox();
Per.parent = a.Root;
Per.color = new(1, 1, 1, 0.5f);
Per.anchor = new(1, 0);
Per.Position = new(new(-100, -100), new(1, 1));
Per.Size = new(new(150, 50), new());
Per.Z = 65536;

//性息文本
var fs1 = assembly.GetManifestResourceStream("SimpleGame.assets.CascadiaMono.ttf");
a.Resource.Create("Font", "Font.CascadiaMono", fs1);
var f1 = a.Resource.GetResource("Font.CascadiaMono") as Font;
f1?.Size = 80;
var PerT = new UIText(a.Resource);
PerT.anchor = new(0.5f, 0.5f);
PerT.Position = new(new(), new(0.5f, 0.5f));
PerT.Size = new(new(), new(1, 1));
PerT.XAlignment = Alignment.Center;
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
    rect.rotation = r;
    icon.rotation = r;
    updD = p.delay;
};

Stopwatch updateTime = new();
updateTime.Start();

a.OnRender += (o, p) =>
{
    if (updateTime.ElapsedMilliseconds >= 1)
    {
        updateTime.Reset();
        updateTime.Start();
        PerT.Text = $"{(uint)(1000f / p.delay) / 1f} FPS";
        Per.Size = new(PerT.GetTextSize(PerT.Text) / new Vector2(1, 1), new());
    }
};

for (int i = 0; i < 1000; i++)
{
    var tmp = new UIBox();
    tmp.parent = a.Root;
}
