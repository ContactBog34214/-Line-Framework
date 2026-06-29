using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI.DefaultWidget;
using Veldrid;

namespace SG;
public static class SimpleGame
{
    static BaseWindow Host;
    static Stopwatch sw = new();
    static readonly float SpinnerBoxSpeed = 3.5f;
    static readonly float SpinnerBoxSize = 400;
    static readonly uint FontSize = 100;
    static Font font;

    public static void Main(string[] args)
    {
        sw.Start();
        Log.SetMinLevel(LogLevel.Info);
        Log.EnableConsole(true);
        Log.SetLogFile(null);
        Log.Info("Welcome to -Line-Framework");
        var assembly = Assembly.GetExecutingAssembly();

        var names = assembly.GetManifestResourceNames();
        foreach (var name in names)
            Log.Debug($"Asset:{name}");

        Host = new(Backend:GraphicsBackend.OpenGLES);
        Host.TargetWindow.Title = "-Line-Framework example";
        Host.UpdatePerSecond=10000;

        Host.Resource.Create(
            "Font",
            "Font",
            assembly.GetManifestResourceStream("SimpleGame.assets.GenJyuuGothic-Normal-2.ttf")
        );
        Log.Debug("Loaded Font");
        font = Host.Resource.GetResource("Font") as Font;
        font?.Size = FontSize;

        Host.Resource.Create(
            "Image",
            "Icon",
            assembly.GetManifestResourceStream("SimpleGame.assets.-L-F.png")
        );
        Log.Debug("Loaded Font");

        var Background = new UIBox
        {
            name = "Background",
            Size = new(new(), new(1, 1)),
            color = new(202f / 255f, 233f / 255f, 1, 1),
            parent = Host.Root,
        };

        var spinnerBox = new UIBox
        {
            name = "SpinnerBox",
            Position = new(new(), new(0.5f)),
            Size = new(new(SpinnerBoxSize), new()),
            Anchor = new(0.5f),
            color = new(153f / 255f, 153f / 255f, 1, 1),
            parent = Background,
        };
        var Image = new UIImage(Host.Resource)
        {
            name = "SpinnerImage",
            Position = new(new(), new(0.5f)),
            Size = new(new(), new(1)),
            Anchor = new(0.5f),
            parent = spinnerBox,
            TextureId = "Icon",
        };

        Host.OnUpdate += (a, b) =>
        {
            float r = sw.ElapsedMilliseconds / 1000f % SpinnerBoxSpeed * 360f / SpinnerBoxSpeed;
            spinnerBox.Rotation = r;
            Image.Rotation = r;
        };

        var title = new UIText(Host.Resource)
        {
            name = "Title",
            Position = new(new(0, 100), new(0.5f, 0)),
            Anchor = new(0.5f),
            parent = Background,
            color = new(105f / 255f, 110f / 255f, 1, 1),
            FontId = "Font",
            XAlignment = Alignment.Center,
            YAlignment = Alignment.Right,
            Text = "-Line-Framework\nExample",
            Z = 1,
        };
        title.Size = new(title.GetTextSize(title.Text) / new Vector2(1, 1), new());

        Host.TargetWindow.FocusGained += () =>
        {
            Host.FramePerSecond=720;
        };
        Host.TargetWindow.FocusLost += () =>
        {
            Host.FramePerSecond=50;
        };

        FPSPrinter();
    }

    static void FPSPrinter()
    {
        var perText = new UIText(Host.Resource)
        {
            name = "PerfText",
            Position = new(new(), new(1)),
            Anchor = new(1),
            parent = Host.Root,
            XAlignment=Alignment.Right,
            YAlignment=Alignment.Right,
            color = new(0,0, 1, 1),
            FontId="Font",
            Z=65536,
            FontScale=0.5f,
        };

        float Renderfps=0;
        float UpdateMs=0;
        Host.OnRender += (a, b) =>
        {
            Renderfps+=(1000f/(float)b-Renderfps)/100f;
        };
        Host.OnUpdate += (a, b) =>
        {
            UpdateMs+=((float)b-UpdateMs)/100f;
            perText.Text=$"{(int)Renderfps}/{Host.FramePerSecond}FPS\n{(int)(UpdateMs*100f)/100f}/{(int)(1000f/Host.UpdatePerSecond*100f)/100f}Ms";
            perText.Size=new(perText.GetTextSize(perText.Text),new());
        };
    }
}
