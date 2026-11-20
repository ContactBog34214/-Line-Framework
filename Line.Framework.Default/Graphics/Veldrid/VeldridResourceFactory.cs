using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed partial class VeldridResourceFactory : IResourceFactory
{
    private readonly GraphicsDevice dev;
    private IGraphicsCapabilities capabilities;

    internal VeldridResourceFactory(GraphicsDevice device)
    {
        dev = device;
    }

    /// <summary>
    /// 该设备的能力查询。
    /// <para>
    /// 资源工厂本身只持有原生 <c>GraphicsDevice</c>（不是
    /// <c>VeldridDevice</c>），所以这里按需构造一份。
    /// 延迟创建一次后常驻 —— 能力查询在创建资源时才会用到，
    /// 且 <see cref="VeldridCapabilities"/> 本身无状态。
    /// </para>
    /// </summary>
    internal IGraphicsCapabilities Capabilities =>
        capabilities ??= new VeldridCapabilities(dev);
}
