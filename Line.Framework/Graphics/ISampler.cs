using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 采样器：控制纹理采样时的过滤与寻址方式。
/// <para>
/// 不拥有资源，可被多个资源集共用。创建资源集时若不给采样器，
/// 后端会使用设备默认采样器（线性过滤 + Clamp 寻址），
/// 这正是 UI 渲染需要的设置。
/// </para>
/// </summary>
public interface ISampler : IDisposable, IName;
