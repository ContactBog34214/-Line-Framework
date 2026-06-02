using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Line.Framework;
using Line.Framework.Audio;
using Line.Framework.Graphics;
using Line.Framework.UI;
using Line.Framework.UI.DefaultWidget;
using TagLib.Riff;
using Veldrid;
using Veldrid.ImageSharp;

Log.SetMinLevel(LogLevel.Debug);
Log.EnableConsole(true);
Log.SetLogFile(null);
Log.Info("Welcome to -Line-Framework");
var assembly = Assembly.GetExecutingAssembly();

var names = assembly.GetManifestResourceNames();
foreach (var name in names) Log.Debug($"Asset:{name}");

//新建窗口
BaseWindow a = new BaseWindow(Backend: GraphicsBackend.OpenGL);
a.FramePerSecond = 144;
a.UpdatePerSecond = 1000;

//正方形底
var rect = new UIButton();
rect.parent = a.Root;
rect.Position = new(new(), new(0.5f, 0.5f));
rect.Size = new(new(350, 350), new());
rect.color = new(0, 42f/255f, 125f/255f, 1);
rect.rotation = 0;
rect.anchor = new(0.5f, 0.5f);

//图标
var icon = new UIImage();
icon.parent = rect;
icon.Position = new(new(), new(0.5f, 0.5f));
icon.anchor = new(0.5f, 0.5f);
icon.Size = new(new(0, 0), new(1, 1));
icon.Color = new(1, 1, 1, 1);
var stream = assembly.GetManifestResourceStream("SimpleGame.assets.-L-F.png");
var image = new ImageSharpTexture(stream);
Texture texture = image.CreateDeviceTexture(a.Dev, a.Dev.ResourceFactory);
icon.LoadTexture(a.Dev, a.RendererClass.TextureLayout, texture);
icon.Z=1;

//文字
var text=new UIText(a.Dev,a.RendererClass.TextureLayout);
text.parent=a.Root;
text.Position=new(new(0,100),new(0.5f,0));
text.anchor=new(0.5f,0.5f);
text.FontSize=100;
text.Text="Welcome To -Line-Framework";
text.LoadFont(assembly.GetManifestResourceStream("SimpleGame.assets.Font.ttf"));
text.Size=new(new(500,100),new());
//text.Text="Welcome";
text.Size=new(text.GetTextSize(text.Text),new());
text.color=new(1,1,1,1);

//开转
Stopwatch sw=new();
sw.Start();
a.OnUpdate += (o, p) =>
{
    var r=sw.ElapsedMilliseconds/1000f%3f;
    r=r/3f*360f;
    rect.rotation=r;
    icon.rotation=r;
};