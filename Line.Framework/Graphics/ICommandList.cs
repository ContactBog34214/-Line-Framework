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
    ///     cl.SetRenderTarget(chain.RenderTarget);
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
        /// 设置渲染目标。
        /// <para>
        /// 对 SDL3 后端，交换链目标会在此刻真正「取」到本帧的纹理；
        /// 取不到时本帧的后续绘制会被静默跳过。
        /// </para>
        /// <para>
        /// 可以在同一次录制里多次调用切换目标（如先渲到离屏再渲到窗口），
        /// 但切换会结束当前的渲染通道，之后必须重新设置管线与资源。
        /// </para>
        /// </summary>
        /// <param name="renderTarget">来自 <see cref="ISwapchain.RenderTarget"/>
        /// 或 <see cref="IResourceFactory.CreateRenderTarget"/>。</param>
        void SetRenderTarget(IRenderTarget renderTarget);

        /// <summary>
        /// 清空指定颜色附件。
        /// <para>
        /// SDL3 的清屏是渲染通道的启动参数，不能在通道中途设置，
        /// 因此必须在 <see cref="SetPipeline"/> 之前调用才会生效。
        /// </para>
        /// </summary>
        void ClearColorTarget(uint index, RgbaFloat color);

        /// <summary>
        /// 清空深度附件。
        /// <para>
        /// <b>必须在 <see cref="SetPipeline"/> 之前调用</b>（SDL3 的清屏是渲染通道
        /// 的启动参数，与颜色清屏相同）。
        /// </para>
        /// <para>
        /// <b>没有调用时的默认行为是清成 <c>1.0</c></b>，不是「保留上一帧内容」。
        /// 深度纹理是持久对象，若默认沿用旧值，新帧会被上一帧的深度挡住 ——
        /// 表现为「某些面永远不出现，且随视角变化换着面消失」，
        /// 看起来像矩阵错或剔除配反，极难定位。
        /// </para>
        /// <para>
        /// 渲染目标没有深度附件时，本调用是空操作。
        /// </para>
        /// </summary>
        /// <param name="depth">
        /// 目标深度值。深度比较默认是 <see cref="CompareFunction.LessOrEqual"/>，
        /// 因此清成 1.0（最远）才能让所有几何通过测试。
        /// </param>
        void ClearDepthTarget(float depth = 1f);

        /// <summary>绑定图形管线。这是两个后端都必须最先完成的状态设置。</summary>
        void SetPipeline(IPipeline pipeline);

        /// <summary>
        /// 绑定顶点缓冲到指定槽位。槽位号对应
        /// <see cref="ShaderSetDescription.VertexLayouts"/> 的下标。
        /// </summary>
        void SetVertexBuffer(uint slot, IDeviceBuffer buffer);

        /// <summary>
        /// 绑定索引缓冲。之后可以用 <see cref="DrawIndexed"/> 按索引绘制。
        /// <para>
        /// <b>offset 的单位是字节</b>，不是索引个数。这与
        /// <see cref="DrawIndexed"/> 的 <c>indexStart</c>（索引个数）是两套单位，
        /// 从同一个大缓冲里画第二段网格时二者只能给一个：
        /// <c>offset = 段起始 * 元素字节数</c> 配 <c>indexStart = 0</c>，
        /// 或 <c>offset = 0</c> 配 <c>indexStart = 段起始</c>。同时给会偏移两倍。
        /// </para>
        /// <para>
        /// 索引缓冲创建时必须声明 <see cref="BufferUsage.Index"/>，
        /// 否则后端会拒绝（SDL3 不报错但绘制结果为空）。
        /// SDL3 的原生绑定参数是「单个引用传参的绑定」，与顶点缓冲的数组形式不同，
        /// 这个差异由后端内部吸收。
        /// </para>
        /// </summary>
        /// <param name="buffer">已按 <see cref="BufferUsage.Index"/> 创建的缓冲。</param>
        /// <param name="format">索引元素宽度。</param>
        /// <param name="offset">缓冲内起始字节偏移。</param>
        void SetIndexBuffer(IDeviceBuffer buffer, IndexFormat format, int offset = 0);

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
        /// 按索引缓冲绘制。
        /// <para>
        /// 参数顺序与两个后端的原生签名一致，因此后端实现是直传：
        /// Veldrid 是 <c>DrawIndexed(indexCount, instanceCount, indexStart, vertexOffset, instanceStart)</c>，
        /// SDL3 是 <c>SDL_DrawGPUIndexedPrimitives(num_indices, num_instances, first_index, vertex_offset, first_instance)</c>。
        /// </para>
        /// <para>
        /// <b>单位约定</b>：<paramref name="indexStart"/> 是<b>索引个数</b>；
        /// <see cref="SetIndexBuffer"/> 的 offset 是<b>字节</b>。
        /// <paramref name="vertexOffset"/> 是加到每个索引上的有符号整数，
        /// 用于共享一个大顶点缓冲里的多段网格 —— 它可以为负。
        /// </para>
        /// </summary>
        /// <param name="indexCount">要绘制的索引数。</param>
        /// <param name="instanceCount">实例数，不实例化时传 1。</param>
        /// <param name="indexStart">起始索引序号（不是字节）。</param>
        /// <param name="vertexOffset">加到索引上的顶点偏移，可为负。</param>
        /// <param name="instanceStart">起始实例偏移。</param>
        void DrawIndexed(
            uint indexCount,
            uint instanceCount,
            uint indexStart,
            int vertexOffset,
            uint instanceStart
        );

        /// <summary>
        /// 设置视口：NDC 映射到渲染目标的哪一块像素区域。
        /// <para>
        /// <b>像素坐标，左上原点，宽高必须为正</b>。默认视口是整个渲染目标。
        /// </para>
        /// <para>
        /// <b>不提供 index 参数</b>（与 <see cref="SetScissorRect"/> 不同）：
        /// SDL3 的渲染通道只有一个视口，多视口无法被最弱的后端实现，
        /// 因此不进公共接口。这一点是刻意的 —— 与其给一个在某后端被忽略的
        /// 参数，不如根本不暴露这个能力。
        /// </para>
        /// <para>
        /// <b>状态生命周期</b>：视口属于「渲染通道 + 管线」这一层状态。
        /// 换渲染目标（<see cref="SetRenderTarget"/>）之后必须重新设置；
        /// 只换管线是否保留取决于后端，因此**每次设完管线都重设一次**最稳妥。
        /// </para>
        /// <para>
        /// <b>深度范围</b>：<paramref name="minDepth"/> / <paramref name="maxDepth"/>
        /// 是 NDC 深度到深度缓冲的映射区间，常规取 0..1。反转深度
        /// （配合 <see cref="CompareFunction.GreaterOrEqual"/>）时传 1..0。
        /// </para>
        /// </summary>
        /// <param name="x">视口左上角 X（像素）。</param>
        /// <param name="y">视口左上角 Y（像素）。</param>
        /// <param name="width">视口宽度（像素），必须 &gt; 0。</param>
        /// <param name="height">视口高度（像素），必须 &gt; 0。</param>
        /// <param name="minDepth">NDC 深度映射的下界。</param>
        /// <param name="maxDepth">NDC 深度映射的上界。</param>
        void SetViewport(
            float x,
            float y,
            float width,
            float height,
            float minDepth = 0f,
            float maxDepth = 1f
        );

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
