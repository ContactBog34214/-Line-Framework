namespace Line.Framework.Graphics;

public interface IResourceFectory
{
    IShader CreateShader(ShaderCreateInfo createInfo);
    IFrameBuffer CreateFrameBuffer();
}