using Line.Framework.UI;

namespace Line.Framework.Graphics;

public interface ICompositor
{
    Task<Vertex[]> Composite(UIWidget Root);
}
