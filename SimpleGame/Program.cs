using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;
using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
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
            Size = new Coord2(new(), new(1, 1)),
            color = new(202f / 255f, 233f / 255f, 1, 1),
            Parent = Host.Root,
        };

        var spinnerBox = new UIBox
        {
            Name = "SpinnerBox",
            Position = new Coord2(new(), new(0.5f)),
            Size = new Coord2(new(SpinnerBoxSize), new()),
            Anchor = new Vector2(0.5f),
            color = new(153f / 255f, 153f / 255f, 1, 1),
            Parent = Background,
            TouchMode = TouchModes.All,
        };
        var Image = new UIImage(Host.Resource)
        {
            Name = "SpinnerImage",
            Position = new Coord2(new(), new(0.5f)),
            Size = new Coord2(new(), new(1)),
            Anchor = new Vector2(0.5f),
            Parent = spinnerBox,
            TextureId = "Icon",
            TouchMode = TouchModes.None,
        };

        var cs = new UIImage(Host.Resource)
        {
            Name = "Cursor",
            Position = new Coord2(new(), new()),
            Size = new Coord2(new(32), new()),
            Anchor = new Vector2(0.5f),
            Parent = Host.Root,
            TextureId = "Cursor",
            Index = 32767,
            TouchMode = TouchModes.None,
        };

        var title = new UIText(Host.Resource)
        {
            Name = "Title",
            Position = new Coord2(new(0, 100), new(0.5f, 0)),
            Anchor = new Vector2(0.5f),
            Parent = Background,
            color = new(105f / 255f, 110f / 255f, 1, 1),
            FontId = "Font",
            XAlignment = Alignment.Center,
            YAlignment = Alignment.Right,
            Text = "-Line- Framework\nExample",
            Index = 1,
            FontSize = 60,
        };
        title.Size = new Coord2(title.GetTextSize(title.Text) / new Vector2(1, 1), new());

        Host.OnUpdate += (a, b) =>
        {
            cs.Position = new Coord2(Host.Input.Mouse.Position, new());
        };
        Host.OnRender += (a, b) =>
        {
            float r = sw.ElapsedMilliseconds / 1000f % SpinnerBoxSpeed * 360f / SpinnerBoxSpeed;
            spinnerBox.Rotation = r;
            Image.Rotation = r;
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
        Host.ParallelRender = false;

        FPSPrinter();

        //PerTest(200000,Host.Root);

        Host.ShowCursor = false;
        Host.ParallelRender = true;
        Host.EnableMouseRelative = true;
        Host.MouseSpeedScale = 1;

        var input = new UIInput(Host.Resource)
        {
            Name = "Input",
            Position = new Coord2(new(0, -120), new(0.5f, 1)),
            Size = new Coord2(new(400, 160), new()),
            Anchor = new Vector2(0.5f),
            Parent = Host.Root,
            Index = 100,
            FontId = "Font",
            CursorColor = new(1, 1, 1, 0.5f),
            FontSize = 50,
            Text = "Wowabcdefghijklmnopq\nwow",
            Offset = new(0),
        };

        VisualTouch();
        Host.Scale = 1f;
        /*
        FileManager fm = new("/home/smellyfish/Documents/Projects/FMTest");
        fm.CompressFile = true;
        fm.CreateFile("test.file");
        var dt = GenerateTestData(4096);
        fm.WriteAllText("test.file", dt);
        fm.ForceClearCache();
        Log.Info(fm.ReadAllText("test.file") == dt);

        Stopwatch sw1 = new();
        fm.AllowCache=true;
        fm.ForceClearCache();
        sw1.Start();
        for (int i = 0; i < 32; i++)
        {
            string text = fm.ReadAllText("test.file");
            if (text != dt)
            {
                Log.Error($"值不匹配:{i}");
            }
        }
        sw1.Stop();
        Log.Info(sw1.ElapsedMilliseconds/1000f);
        */
    }

    static string GenerateTestData(int repeatCount)
    {
        var lines = new[]
        {
            "Hello, this is a test string for compression algorithms.",
            "It contains repeated sentences to achieve high compression ratio.",
            "You can modify the content or repeat count to suit your test.",
            "Brotli, GZip, and LZMA all perform well on such data.",
            "The quick brown fox jumps over the lazy dog.",
        };
        var sb = new StringBuilder();
        for (int i = 0; i < repeatCount; i++)
        {
            foreach (var line in lines)
                sb.AppendLine(line);
        }
        return sb.ToString();
    }

    static void FPSPrinter()
    {
        var perText = new UIText(Host.Resource)
        {
            Name = "PerfText",
            Position = new Coord2(new(), new(1)),
            Anchor = new Vector2(1),
            Parent = Host.Root,
            XAlignment = Alignment.Right,
            YAlignment = Alignment.Right,
            color = new(0, 0, 1, 1),
            FontId = "Font",
            Index = 65536,
            FontSize = 40,
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
            perText.Size = new Coord2(perText.GetTextSize(perText.Text), new());
        };
    }

    static void PerTest(uint num, UIWidget root)
    {
        for (int i = 0; i < num; i++)
        {
            _ = new UIBox()
            {
                Name = $"_PerTest",
                Index = -100,
                Parent = root,
                Visible = true,
            };
        }
    }

    static void VisualTouch()
    {
        var TouchC = new UIBox()
        {
            Name = "TouchC",
            Size = new Coord2(new(), new(1)),
            Parent = Host.Root,
            Index = 1000,
            color = new(0, 0, 0, 0),
            TouchMode = TouchModes.None,
        };

        Host.Input.FingerDown += (a) =>
        {
            _ = new UICircle()
            {
                Name = $"{a.Id}",
                Position = new Coord2(a.Finger.Position, new()),
                Size = new Coord2(new(25, 25), new()),
                Anchor = new Vector2(0.5f),
                Parent = TouchC,
                Index = a.Id,
                color = new(1, 1, 1, 0.5f),
            };
        };
        Host.Input.FingerMove += (a) =>
        {
            var tg = TouchC.FindChildren($"{a.Id}");
            if (tg.Count > 0 && tg[0] is UICircle b)
            {
                b.Position = new Coord2(a.Finger.Position, new());
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
