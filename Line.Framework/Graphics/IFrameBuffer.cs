namespace Line.Framework.Graphics;

public interface IFrameBuffer : IDisposable
{
    string Name { get; }
    uint Width { get; }
    uint Height { get; }
}