using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    /// <summary>
    /// 命令列表：一帧绘制指令的录制容器。
    /// <para>
    /// <b>使用范式</b>（两个后端通用）：
    /// <code>
    /// using var cl = device.ResourceFactory.CreateCommandList();
    /// using (cl.Record())
    /// {
    ///     cl.SetGraphicsResourceSet(0, set);   // 顺序随意，见下
    ///     cl.SetFrameBuffer(chain.FrameBuffer);
    ///     cl.ClearColorTarget(0, color);
    ///     cl.SetPipeline(pipeline);
    ///     cl.SetVertexBuffer(0, vb);
    ///     cl.Draw(count, 1, 0, 0);
    /// }
    /// device.SubmitCommands(cl);
    /// chain.Swap();
    /// </code>
    /// </para>
    /// <para>
    /// <b>关于绑定顺序</b>：两个后端对「资源集何时可以绑定」的要求是互斥的
    /// （Veldrid 要求先有活跃管线；SDL3 的 uniform 必须在渲染通道之外推送），
    /// 因此各自在内部做了延迟应用。结果是调用方可以自由选择顺序，
    /// 不需要为任何后端特化。
    /// </para>
    /// </summary>
    public interface ICommandList : IDisposable, IName
    {
        /// <summary>
        /// 是否处于录制状态。true = 开始录制，false = 结束录制（但不提交，
        /// 提交由 <see cref="IGraphicsDevice.SubmitCommands"/> 完成）。
        /// </summary>
        bool Working { get; set; }

        /// <summary>
        /// 开始录制并返回一个作用域，Dispose 时结束录制。
        /// 比直接赋 <see cref="Working"/> 更安全（异常时也能正确收尾）。
        /// </summary>
        IDisposable Record();

        /// <summary>
        /// 设置渲染目标。SDL3 后端在此刻真正「取」到本帧的交换链纹理，
        /// 取不到时本帧的后续绘制会被静默跳过。
        /// </summary>
        void SetFrameBuffer(IFrameBuffer frameBuffer);

        /// <summary>
        /// 清空指定颜色附件。
        /// <para>
        /// SDL3 的清屏是渲染通道的启动参数，不能在通道中途设置，
        /// 因此必须在 <see cref="SetPipeline"/> 之前调用才会生效。
        /// </para>
        /// </summary>
        void ClearColorTarget(uint index, RgbaFloat color);

        /// <summary>绑定图形管线。这是两个后端都必须最先完成的状态设置。</summary>
        void SetPipeline(IPipeline pipeline);

        /// <summary>
        /// 绑定顶点缓冲到指定槽位。槽位号对应
        /// <see cref="ShaderSetDescription.VertexLayouts"/> 的下标。
        /// </summary>
        void SetVertexBuffer(uint slot, IDeviceBuffer buffer);

        /// <summary>
        /// 绑定资源集到指定槽位（即 <see cref="PipelineCreateInfo.ResourceLayouts"/> 的下标）。
        /// 对 SDL3 而言，这一步还会把 uniform 数据推送进命令缓冲。
        /// </summary>
        void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet);

        /// <summary>
        /// 绘制实例化图元。
        /// </summary>
        /// <param name="vertexCount">每个实例的顶点数。</param>
        /// <param name="instanceCount">实例数。</param>
        /// <param name="vertexStart">起始顶点偏移（可用来从一个静态缓冲里截取某段）。</param>
        /// <param name="instanceStart">起始实例偏移。</param>
        void Draw(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart);

        /// <summary>
        /// 设置裁剪矩形（屏幕坐标，左上原点）。
        /// <para>
        /// 抽象层没有视口设置，默认视口即整个渲染目标；
        /// 需要局部绘制时用本方法裁剪。
        /// </para>
        /// </summary>
        void SetScissorRect(uint index, uint x, uint y, uint width, uint height);
    }
}
