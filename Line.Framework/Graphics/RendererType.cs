using Line.Framework.IO;
using Veldrid;

namespace Line.Framework.Graphics;

public abstract class RendererType : IDisposable
{
    public virtual ResourceLayout TextureLayout { get; }
    public abstract void Dispose();
    public abstract void Render(Vertex[] vertices);
    protected virtual WindowType Host { get; }

    protected RendererType(WindowType window)
    {
        Host = window;
    }
}
