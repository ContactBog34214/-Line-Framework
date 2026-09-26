using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface ISwapchain : IDisposable,IName
{
    void Resize(int width, int height);
    IFrameBuffer FrameBuffer { get; }
}