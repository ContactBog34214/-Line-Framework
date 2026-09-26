using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridDevice : IGraphicsDevice
{
    private readonly GraphicsDevice graphicsDevice;
    public string Name => graphicsDevice.DeviceName;
    public VeldridDevice(GraphicBackend backend)
    {
        GraphicsDeviceOptions options = new()
        {
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = false,
        };
        switch (backend)
        {
            case GraphicBackend.Vulkan:
                graphicsDevice = GraphicsDevice.CreateVulkan(options);
                break;
            case GraphicBackend.Metal:
                graphicsDevice = GraphicsDevice.CreateMetal(options);
                break;
            case GraphicBackend.Direct3D:
                graphicsDevice = GraphicsDevice.CreateD3D11(options);
                break;
            case GraphicBackend.OpenGL:
                throw new NotSupportedException();
        }
        ResourceFectory = new VeldridResourceFactory(graphicsDevice);
    }

    public IResourceFactory ResourceFectory { get; }

    public void Dispose()
    {
        graphicsDevice?.Dispose();
    }

    public void WaitForIdle()
    {
        graphicsDevice?.WaitForIdle();
    }
}