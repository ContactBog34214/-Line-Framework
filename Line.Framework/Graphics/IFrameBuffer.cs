using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IFrameBuffer : IDisposable,IName
{
    uint Width { get; }
    uint Height { get; }
}