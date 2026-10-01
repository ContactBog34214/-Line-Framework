using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridSampler : ISampler
{
    internal readonly Sampler sampler;
    public string Name => sampler.Name;
    internal VeldridSampler(Sampler s)
    {
        sampler = s;
    }
    public void Dispose()
    {
        sampler?.Dispose();
    }
}