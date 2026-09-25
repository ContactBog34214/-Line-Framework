namespace Line.Framework.Graphics;

public interface IGraphicsDevice : IDisposable
{
    void WaitForIdle();
    IResourceFectory ResourceFectory { get; }
}