using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IGraphicsDevice : IDisposable, IName
{
    void WaitForIdle();
    void SubmitCommands(ICommandList commandList);
    IResourceFactory ResourceFactory { get; }
}
