using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Resource.Graphic;
using Line.Framework.UI;
using Line.Framework.UI.DefaultWidget;
using SDL3;
#pragma warning disable CS8618

namespace SG;

public static class SimpleGame
{
    static Window Host;
    static Stopwatch sw = new();
    static readonly float SpinnerBoxSpeed = 3.5f;
    static readonly float SpinnerBoxSize = 400;
    static readonly uint FontSize = 100;
    static Font font;

    public static void Main()
    {
        sw.Start();
        Log.SetMinLevel(LogLevel.Debug);
        Log.EnableConsole(true);
        Log.SetLogFile(null);
        Log.Info("Welcome to -Line-Framework");
        var assembly = Assembly.GetExecutingAssembly();

        var names = assembly.GetManifestResourceNames();
        foreach (var name in names)
            Log.Debug($"Asset:{name}");

        Host = new(Backend: GraphicBackend.OpenGL)
        {
            Title = "-Line-Framework example",
            UpdatePerSecond = 10000,
        };

        Host.Resource.Create(
            "Font",
            "Font",
            assembly.GetManifestResourceStream("SimpleGame.assets.GenJyuuGothic-Normal-2.ttf")
        );

        //Exp
        Log.Debug("Loaded Font");
        font = Host.Resource?.GetResource("Font") as Font;
        font?.Size = FontSize;

        Host.Resource?.Create(
            "Image",
            "Icon",
            assembly.GetManifestResourceStream("SimpleGame.assets.-L-F.png")
        );
        Host.Resource?.Create(
            "Image",
            "Cursor",
            assembly.GetManifestResourceStream("SimpleGame.assets.cursor.png")
        );
        Log.Debug("Loaded Font");

        var Background = new UIBox
        {
            Name = "Background",
            Size = new(new(), new(1, 1)),
            color = new(202f / 255f, 233f / 255f, 1, 1),
            Parent = Host.Root,
        };

        var spinnerBox = new UIBox
        {
            Name = "SpinnerBox",
            Position = new(new(), new(0.5f)),
            Size = new(new(SpinnerBoxSize), new()),
            Anchor = new(0.5f),
            color = new(153f / 255f, 153f / 255f, 1, 1),
            Parent = Background,
            TouchMode = TouchModes.All,
        };
        var Image = new UIImage(Host.Resource)
        {
            Name = "SpinnerImage",
            Position = new(new(), new(0.5f)),
            Size = new(new(), new(1)),
            Anchor = new(0.5f),
            Parent = spinnerBox,
            TextureId = "Icon",
            TouchMode = TouchModes.None,
        };

        var cs = new UIImage(Host.Resource)
        {
            Name = "Cursor",
            Position = new(new(), new()),
            Size = new(new(32), new()),
            Anchor = new(0.5f),
            Parent = Host.Root,
            TextureId = "Cursor",
            Z = 32767,
            TouchMode = TouchModes.None,
        };

        var title = new UIText(Host.Resource)
        {
            Name = "Title",
            Position = new(new(0, 100), new(0.5f, 0)),
            Anchor = new(0.5f),
            Parent = Background,
            color = new(105f / 255f, 110f / 255f, 1, 1),
            FontId = "Font",
            XAlignment = Alignment.Center,
            YAlignment = Alignment.Right,
            Text = "-Line-Framework\nExample",
            Z = 1,
        };
        title.Size = new(title.GetTextSize(title.Text) / new Vector2(1, 1), new());

        Host.OnUpdate += (a, b) =>
        {
            float r = sw.ElapsedMilliseconds / 1000f % SpinnerBoxSpeed * 360f / SpinnerBoxSpeed;
            spinnerBox.Rotation = r;
            Image.Rotation = r;
            cs.Position = new(Host.Input.Mouse.Position, new());
        };

        Host.FramePerSecond = 5000;
        Host.FocusGained += () =>
        {
            Host.VSync = false;
        };
        Host.FocusLost += () =>
        {
            Host.VSync = true;
        };

        Host.UpdatePerSecond = 1000;

        FPSPrinter();

        //PerTest(200000,Host.Root);

        Host.ShowCursor = false;
        Host.ParallelRender = true;
        Host.EnableMouseRelative = true;

        var input = new UIInput(Host.Resource)
        {
            Name = "Input",
            Position = new(new(0, -120), new(0.5f, 1)),
            Size = new(new(400, 160), new()),
            Anchor = new(0.5f),
            Parent = Host.Root,
            Z = 100,
            FontId = "Font",
            CursorColor = new(1, 1, 1, 0.5f),
            FontScale = 1f,
            Text = "Wowabcdefghijklmnopq\nwow",
            Offset = new(0),
        };

        VisualTouch();
        Host.Scale = 1f;
    }

    static void FPSPrinter()
    {
        var perText = new UIText(Host.Resource)
        {
            Name = "PerfText",
            Position = new(new(), new(1)),
            Anchor = new(1),
            Parent = Host.Root,
            XAlignment = Alignment.Right,
            YAlignment = Alignment.Right,
            color = new(0, 0, 1, 1),
            FontId = "Font",
            Z = 65536,
            FontScale = 0.5f,
        };

        float Renderfps = 0;
        float UpdateMs = 0;
        float Rf = 0;
        Host.OnRender += (a, b) =>
        {
            Rf = 1000f / (float)b;
        };
        Host.OnUpdate += (a, b) =>
        {
            UpdateMs += ((float)b - UpdateMs) / 200f;
            Renderfps += (Rf - Renderfps) / 200f;
            perText.Text =
                $"{(int)Renderfps}/{Host.FramePerSecond}FPS\n{(int)(UpdateMs * 100f) / 100f}/{(int)(1000f / Host.UpdatePerSecond * 100f) / 100f}Ms";
            perText.Size = new(perText.GetTextSize(perText.Text), new());
        };
    }

    static void PerTest(uint num, UIWidget root)
    {
        for (int i = 0; i < num; i++)
        {
            _ = new UIBox()
            {
                Name = $"_PerTest",
                Z = -100,
                Parent = root,
                visible = true,
            };
        }
    }

    static void VisualTouch()
    {
        var TouchC = new UIBox()
        {
            Name = "TouchC",
            Size = new(new(), new(1)),
            Parent = Host.Root,
            Z = 1000,
            color = new(0, 0, 0, 0),
            TouchMode = TouchModes.None,
        };

        Host.Input.FingerDown += (a) =>
        {
            _ = new UICircle()
            {
                Name = $"{a.Id}",
                Position = new(a.Finger.Position, new()),
                Size = new(new(25, 25), new()),
                Anchor = new(0.5f),
                Parent = TouchC,
                Z = a.Id,
                color = new(1, 1, 1, 0.5f),
            };
        };
        Host.Input.FingerMove += (a) =>
        {
            var tg = TouchC.FindChildren($"{a.Id}");
            if (tg.Count > 0 && tg[0] is UICircle b)
            {
                b.Position = new(a.Finger.Position, new());
            }
        };
        Host.Input.FingerUp += (a) =>
        {
            var tg = TouchC.FindChildren($"{a.Id}");
            if (tg.Count > 0 && tg[0] is UICircle b)
            {
                b.Dispose();
            }
        };
    }
}
