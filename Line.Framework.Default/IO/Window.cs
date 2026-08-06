using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Line.Framework.Graphics;
using Line.Framework.IO;
using Line.Framework.Resource;
using Line.Framework.Resource.Audio;
using Line.Framework.Resource.Graphic;
using Line.Framework.Types;
using Line.Framework.UI;
using SDL3;
using Veldrid;
using Veldrid.OpenGL;

namespace Line.Framework.Default.Graphics;

public class Window : WindowType
{
    public override RendererType Renderer { get; }
    public override ICompositor Compositor { get; }

    public Window(
        int X = 0,
        int Y = 0,
        int Width = 640,
        int Height = 480,
        WindowState State = WindowState.Normal,
        GraphicBackend? Backend = null,
        string Title = "Title"
    )
        : base(X, Y, Width, Height, State, Backend, Title)
    {
        if (Renderer == null)
            Renderer = new Renderer(this);
        if (Compositor == null)
            Compositor = new Compositor();
        Resource.AddType("Image", new TResourceSet(Resource, Dev, Renderer.TextureLayout));
        Resource.AddType("Font", new TFont(Resource, Dev, Renderer.TextureLayout));
    }
}
