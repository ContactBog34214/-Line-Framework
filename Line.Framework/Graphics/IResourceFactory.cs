namespace Line.Framework.Graphics;

public interface IResourceFactory
{
    IShader CreateShader(ShaderCreateInfo createInfo);
    IFrameBuffer CreateFrameBuffer();
}