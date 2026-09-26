using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IGraphicsDevice : IDisposable, IName
{
    void WaitForIdle();
    IResourceFactory ResourceFectory { get; }
}