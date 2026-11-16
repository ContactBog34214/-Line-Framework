using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 资源布局：描述「一个资源集里有哪些槽位、各自的类型与可见阶段」。
/// <para>
/// 布局只描述形状，不持有资源；实际资源由
/// <see cref="IResourceFactory.CreateResourceSet"/> 按布局装配成 <see cref="IResourceSet"/>。
/// 布局应当长期复用（创建昂贵，且资源集必须与创建它的布局匹配）。
/// </para>
/// <para>
/// <b>槽位编号规则由后端统一实现</b>：元素按 <see cref="ResourceLayoutElement.Kind"/>
/// 分组，组序固定为 采样器 → 可写纹理 → storage buffer → uniform buffer，
/// 组内按声明顺序从 0 连续编号。因此同类元素之间的先后有意义，
/// 不同类之间的先后会被重排。详见 <see cref="ResourceLayoutCreateInfo"/>。
/// </para>
/// </summary>
public interface IResourceLayout : IName, IDisposable
{
    /// <summary>
    /// 声明过的元素（抽象层视角，未展开）。
    /// <para>
    /// 注意 <see cref="ResourceKind.TextureSampler"/> 在后端通常会展开成
    /// 「纹理 + 采样器」两个底层槽位，因此这里的长度可能小于后端的实际槽位数。
    /// </para>
    /// </summary>
    ReadOnlySpan<ResourceLayoutElement> Elements { get; }
}
