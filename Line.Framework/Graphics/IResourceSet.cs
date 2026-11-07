using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    public interface IResourceSet : IDisposable, IName
    {
        IResourceLayout Layout { get; }
    }
}
