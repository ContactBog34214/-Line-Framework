using Line.Framework.Graphics;
using Veldrid;
using Veldrid.SPIRV;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridResourceFactory : IResourceFactory
{
    private readonly GraphicsDevice dev;
    public IShader CreateShader(ShaderCreateInfo createInfo)
    {
        var shader = dev.ResourceFactory.CreateFromSpirv(new()
        {
            EntryPoint = createInfo.EntryPoint,
            Stage = VeldridShader.GetStages(createInfo.Stage),
            ShaderBytes = createInfo.Bytes
        });
        return new VeldridShader(shader);
    }

    public IFrameBuffer CreateFrameBuffer()
    {
        throw new NotImplementedException();
    }

    internal VeldridResourceFactory(GraphicsDevice device)
    {
        dev = device;
    }
}