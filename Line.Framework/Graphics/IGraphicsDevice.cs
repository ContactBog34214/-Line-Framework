namespace Line.Framework.Graphics;

public interface IGraphicsDevice : IDisposable
{
    void WaitForIdle();
    IResourceFactory ResourceFectory { get; }
}