using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid
{
    public class VeldridResourceLayout : IResourceLayout
    {
        internal ResourceLayout ResourceLayout { get; }
        public string Name => ResourceLayout.Name;

        public void Dispose()
        {
            ResourceLayout?.Dispose();
            //GC.SuppressFinalize(this);
        }
    }
}
