using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed partial class VeldridResourceFactory : IResourceFactory
{
    private readonly GraphicsDevice dev;

    internal VeldridResourceFactory(GraphicsDevice device)
    {
        dev = device;
    }
}
