namespace Line.Framework.Graphics;

/// <summary>
/// 管线类型。
/// <para>
/// <see cref="Compute"/> 是为将来预留的枚举值，<b>当前没有任何后端实现</b>：
/// 抽象层的 <see cref="ICommandList"/> 没有分派入口，两个后端的
/// <see cref="IResourceFactory.CreatePipeline"/> 遇到它都会抛
/// <see cref="NotSupportedException"/>。
/// </para>
/// <para>
/// 之所以不删：将来接入计算时它必须与图形管线共存于同一个枚举
/// （管线创建入口是共用的）。但请勿据此假设它可用。
/// </para>
/// </summary>
public enum PipelineType
{
    /// <summary>图形管线：顶点 → 光栅化 → 片元。</summary>
    Graphics,

    /// <summary>计算管线（尚未实现）。</summary>
    Compute,
}
