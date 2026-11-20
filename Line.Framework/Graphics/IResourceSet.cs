using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    /// <summary>
    /// 资源集：按某个布局把实际资源装配成可直接绑定的整体。
    /// <para>
    /// 创建昂贵（Vulkan 上会从描述符池分配），应长期复用；
    /// 每帧重建资源集很快就会成为瓶颈。
    /// </para>
    /// <para>
    /// <b>生命周期</b>：先释放资源集，再释放它引用的纹理 / 缓冲 / 采样器 ——
    /// 资源集本身不拥有它们。
    /// </para>
    /// </summary>
    public interface IResourceSet : IDisposable, IName
    {
        /// <summary>创建时使用的布局。资源集只能绑定到兼容此布局的管线上。</summary>
        IResourceLayout Layout { get; }
    }
}
