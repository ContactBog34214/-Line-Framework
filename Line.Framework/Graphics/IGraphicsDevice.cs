using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 图形设备：一个后端实例，持有 GPU 上下文与资源工厂。
/// <para>
/// 生命周期上它是最外层的对象：先创建设备，再由
/// <see cref="ResourceFactory"/> 创建交换链与所有资源。
/// </para>
/// <para>
/// <b>建链顺序的后端差异</b>：Veldrid 在创建设备时必须同时提供交换链描述
///（否则无法判断哪个队列族支持呈现），因此它的构造器通过 out 参数返回第一张交换链；
/// SDL3 则可以先建设备再建交换链。需要统一时，请用各自后端的工厂入口，
/// 不要把两条路径混在同一段代码里。
/// </para>
/// </summary>
public interface IGraphicsDevice : IDisposable, IName
{
    /// <summary>
    /// 阻塞直到 GPU 完成已提交的工作。
    /// <para>
    /// 用于在改变交换链尺寸、释放仍可能被 GPU 引用的资源之前同步。
    /// <b>会显著打断管线</b>，不要放在每帧路径里。
    /// </para>
    /// </summary>
    void WaitForIdle();

    /// <summary>
    /// 提交一份命令列表。
    /// <para>
    /// SDL3 后端的「提交」同时完成呈现，因此
    /// <see cref="ISwapchain.Swap"/> 在该后端是空操作；
    /// Veldrid 则需在提交后单独调用 Swap。
    /// </para>
    /// </summary>
    /// <param name="commandList">已录制完成的命令列表。</param>
    void SubmitCommands(ICommandList commandList);

    /// <summary>本设备的资源工厂：创建纹理、缓冲、管线、交换链等。</summary>
    IResourceFactory ResourceFactory { get; }
}
