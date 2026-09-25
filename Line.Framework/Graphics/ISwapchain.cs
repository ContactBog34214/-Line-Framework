namespace Line.Framework.Graphics;

public interface ISwapchain : IDisposable
{
    string Name { get; }
    void Resize(int width, int height);
    IFrameBuffer FrameBuffer { get; }
}