using Line.Framework;
using Line.Framework.Graphics;
using Line.Framework.Types;
using SDL3;
using static SDL3.SDL;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// SDL3 命令缓冲的包装。
/// <para>
/// <b>生命周期与其他后端不同</b>：
/// <list type="bullet">
/// <item><c>Working = true</c> → <c>SDL_AcquireGPUCommandBuffer</c></item>
/// <item><c>Working = false</c> → 结束渲染通道（若有），但<b>不提交</b></item>
/// <item><c>IGraphicsDevice.SubmitCommands</c> → <c>SDL_SubmitGPUCommandBuffer</c>，
///       同时完成「提交 + 呈现」，因此 <c>ISwapchain.Swap()</c> 是空操作</item>
/// </list>
/// </para>
/// </summary>
public sealed class SDL3CommandList : ICommandList
{
    private readonly SDL3Device device;

    private IntPtr cmd;
    private IntPtr renderPass;

    /// <summary>
    /// 本帧的渲染目标纹理。
    /// <para>
    /// 交换链目标每帧从窗口取得一个纹理；离屏目标直接用它持有的纹理。
    /// 两者都归一到这两个句柄上，<see cref="EnsurePass"/> 不需要知道来源。
    /// </para>
    /// </summary>
    private IntPtr targetColor;

    private IntPtr targetDepth;

    /// <summary>
    /// 本命令缓冲内已经 acquire 过的交换链目标。
    /// <para>
    /// <c>SDL_WaitAndAcquireGPUSwapchainTexture</c> 对同一个命令缓冲重复调用
    /// 是无效的（会拿到 0 或重复索取），所以同一次录制里重复
    /// <c>SetRenderTarget(同一个交换链目标)</c> 必须复用第一次的结果。
    /// </para>
    /// </summary>
    /// <summary>当前绑定的渲染目标（用于判断「是否真的换了目标」）。</summary>
    private IRenderTarget activeTarget;

    private SDL3SwapchainTarget acquiredSwapTarget;

    private IntPtr acquiredSwapTexture;

    /// <summary>
    /// 与 <see cref="acquiredSwapTexture"/> 配对的深度纹理句柄。
    /// <para>
    /// 深度纹理可能在尺寸变化时被重建，但同一次录制里必须始终引用
    /// 同一个对象，所以和颜色纹理一起缓存。
    /// </para>
    /// </summary>
    private IntPtr acquiredSwapDepth;

    /// <summary>尚未被渲染通道消费的清屏颜色。</summary>
    private (uint Index, RgbaFloat Color)? pendingClear;

    /// <summary>
    /// 尚未被渲染通道消费的深度清屏值。
    /// <para>
    /// <b>默认是 1.0 而不是 null</b>：深度纹理是持久对象，若沿用上一帧内容，
    /// 新帧会被旧深度挡住（表现为某些面永远不出现且随视角换面）。
    /// 这里记成 null 表示「调用方没指定」，实际建通道时按 1.0 处理。
    /// </para>
    /// </summary>
    private float? pendingDepthClear;

    private bool recording;

    /// <summary>连续拿到空交换链纹理的帧数，用于一次性告警。</summary>
    private static int emptyAcquireCount;

    /// <summary>提交 / present 失败是否已告警（避免每帧刷屏）。</summary>
    private static bool reportedSubmitFailure;

    /// <summary>
    /// 最近一次 SetGraphicsResourceSet 登记的资源集。
    /// <para>
    /// 采样器绑定必须在渲染通道内进行，而调用方可能在通道开启前就绑定资源集，
    /// 因此把它缓存下来，等通道真正开启后再应用（见 ApplyPendingSamplers）。
    /// </para>
    /// </summary>
    private SDL3ResourceSet pendingSet;

    private uint pendingSlot;

    private static string LogicalSizeText(IntPtr window) =>
        SDL.GetWindowSize(window, out var w, out var h) ? $"{w}x{h}" : $"?({SDL.GetError()})";

    private static string PixelSizeText(IntPtr window) =>
        SDL.GetWindowSizeInPixels(window, out var w, out var h) ? $"{w}x{h}" : $"?({SDL.GetError()})";

    public string Name { get; internal set; } = "SDL3CommandList";

    internal SDL3CommandList(SDL3Device dev, string name)
    {
        device = dev;
        if (!string.IsNullOrEmpty(name))
            Name = name;
    }

    // ---------------------------------------------------------------- 录制状态

    public bool Working
    {
        get => recording;
        set
        {
            if (value == recording)
            {
                if (value)
                    throw new InvalidOperationException("Already recording.");
                return;
            }

            if (value)
                BeginRecording();
            else
                EndRecording();
        }
    }

    public IDisposable Record()
    {
        if (recording)
            throw new InvalidOperationException(
                "Already recording; nested Record is not supported."
            );
        Working = true;
        return new RecordScope(this);
    }

    private sealed class RecordScope(SDL3CommandList owner) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0)
                return;
            if (owner.recording)
                owner.Working = false;
        }
    }

    private void BeginRecording()
    {
        cmd = SDL.AcquireGPUCommandBuffer(device.dev);
        if (cmd == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SDL_AcquireGPUCommandBuffer failed: {SDL.GetError()}"
            );

        renderPass = IntPtr.Zero;
        pendingClear = null;
        pendingDepthClear = null;
        targetColor = IntPtr.Zero;
        targetDepth = IntPtr.Zero;
        activeTarget = null;
        acquiredSwapTarget = null;
        acquiredSwapTexture = IntPtr.Zero;
        acquiredSwapDepth = IntPtr.Zero;
        pendingSet = null;
        pendingSlot = 0;
        recording = true;
    }

    private void EndRecording()
    {
        EndPass();
        recording = false;
        // 不提交 —— 由 SubmitCommands 负责
    }

    private void EndPass()
    {
        if (renderPass != IntPtr.Zero)
        {
            SDL.EndGPURenderPass(renderPass);
            renderPass = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 惰性开启渲染通道。
    /// <para>
    /// SDL3 的清屏是 <c>BeginGPURenderPass</c> 的参数，不能在通道中途设置，
    /// 因此 <see cref="ClearColorTarget"/> 只是记录待应用的颜色，
    /// 由本方法在首次需要渲染时一并提交。
    /// </para>
    /// </summary>
    private void EnsurePass()
    {
        if (renderPass != IntPtr.Zero)
            return;

        // 没有有效目标（未调用 SetRenderTarget，或交换链纹理本帧不可用）：
        // 不建通道，后续所有绑定/绘制由各自的 renderPass 判空跳过。
        if (targetColor == IntPtr.Zero)
            return;

        var info = new GPUColorTargetInfo
        {
            Texture = targetColor,
            MipLevel = 0,
            LayerOrDepthPlane = 0,
            ClearColor = pendingClear.HasValue
                ? SDL3Converter.ConvertColor(pendingClear.Value.Color)
                : default,
            LoadOp = pendingClear.HasValue ? GPULoadOp.Clear : GPULoadOp.Load,
            StoreOp = GPUStoreOp.Store,
            ResolveTexture = IntPtr.Zero,
            ResolveMipLevel = 0,
            ResolveLayer = 0,
        };

        GPUColorTargetInfo[] targets = [info];

        if (targetDepth != IntPtr.Zero)
        {
            // LoadOp 必须是 Clear，且默认清 1.0（最远）：
            //   - 深度纹理是持久对象，用 Load 会沿用上一帧的深度值，
            //     新帧被旧深度挡住，表现为「某些面永远不出现且随视角换面」；
            //   - DontCare 等于把结果交给驱动，是随机的。
            // 调用方没调 ClearDepthTarget 时也按 1.0 清，这是抽象层的约定。
            var depthInfo = new GPUDepthStencilTargetInfo
            {
                Texture = targetDepth,
                MipLevel = 0,
                Layer = 0,
                ClearDepth = pendingDepthClear ?? 1f,
                LoadOp = GPULoadOp.Clear,
                StoreOp = GPUStoreOp.Store,
                ClearStencil = 0,
                // 目前只用带模板的深度格式，但模板测试一律关闭，
                // 因此 StencilLoadOp 用 DontCare 足够（不会有人读它）。
                StencilLoadOp = GPULoadOp.DontCare,
                StencilStoreOp = GPUStoreOp.DontCare,
                // SDL3-CS 把原生 bool 生成为 byte（与 GPUMultisampleState.EnableMask
                // 同一情况），0 = false。cycle 表示「纹理被绑定时是否孤儿化」，
                // 深度纹理是持久复用的，不需要。
                Cycle = 0,
            };

            renderPass = SDL.BeginGPURenderPass(cmd, targets, 1, depthInfo);
        }
        else
        {
            renderPass = SDL.BeginGPURenderPass(cmd, targets, 1, IntPtr.Zero);
        }

        pendingClear = null;
        pendingDepthClear = null;

        if (renderPass == IntPtr.Zero)
            return;

        // 不在这里设 viewport / scissor。
        //
        // SDL3 的渲染通道默认视口 = 整个 color target，默认 scissor = 全域，
        // 因此全屏绘制（UI 合成器的情形）无需手动设置。
        //
        // 需要局部视口 / 裁剪时调用 SetViewport / SetScissorRect —— 它们都
        // 要求通道已开启，所以内部各自先调 EnsurePass。
        //
        // 注意：通道重建（换目标）后 SDL 会把视口重置回默认值，
        // 调用方换目标后必须重新调用 SetViewport。
        //
        // 另外 SDL_SetGPUViewport / SDL_SetGPUScissor 内部会断言
        // "Render pass not in progress!"，一旦通道状态与 SDL 内部不一致，
        // 断言直接 abort（退出码 134），比不设更危险 —— 因此这些调用
        // 必须发生在 renderPass 确实非零之后。

        // 通道刚开启：把之前登记但还没能应用的采样器绑定补上。
        // 调用方在 SetRenderTarget 之前绑定资源集时，走的就是这条路径。
        ApplyPendingSamplers();
    }

    // ---------------------------------------------------------------- 状态绑定

    public void SetRenderTarget(IRenderTarget renderTarget)
    {
        if (!recording || cmd == IntPtr.Zero)
            throw new InvalidOperationException(
                "SetRenderTarget must be called after Working = true."
            );

        // 换目标必须先结束当前通道
        EndPass();

        switch (renderTarget)
        {
            case SDL3SwapchainTarget sc:
                SetSwapchainTarget(sc);
                break;

            case SDL3RenderTarget rt:
                if (rt.PrimaryColorHandle == IntPtr.Zero)
                    throw new ObjectDisposedException(
                        nameof(SDL3RenderTarget),
                        "The render target's color texture has been released."
                    );
                targetColor = rt.PrimaryColorHandle;
                targetDepth = rt.DepthHandle;
                break;

            default:
                throw new ArgumentException(
                    $"Render target {renderTarget?.GetType().Name ?? "null"} "
                        + "is not an SDL3 render target.",
                    nameof(renderTarget)
                );
        }

        // 换目标（而不是重复设置同一个）时才丢弃已登记的清屏值：
        // 让 SetRenderTarget → ClearColorTarget → SetRenderTarget(同一目标)
        // 这种写法不至于把清屏值弄丢。
        if (!ReferenceEquals(activeTarget, renderTarget))
        {
            pendingClear = null;
            pendingDepthClear = null;
        }

        activeTarget = renderTarget;
    }

    /// <summary>
    /// 从交换链取得本帧的纹理句柄。
    /// <para>
    /// 同一个命令缓冲只能 acquire 一次，因此重复设置同一个交换链目标时
    /// 复用第一次的结果。否则第二次调用会拿到 0 或索取一个尚不可用的纹理。
    /// </para>
    /// </summary>
    private void SetSwapchainTarget(SDL3SwapchainTarget sc)
    {
        if (ReferenceEquals(acquiredSwapTarget, sc))
        {
            targetColor = acquiredSwapTexture;
            targetDepth = acquiredSwapDepth;
            return;
        }

        // SDL3 的交换链纹理是在命令缓冲上「取」出来的，
        // 且只能由取它的那个命令缓冲引用。
        if (
            !SDL.WaitAndAcquireGPUSwapchainTexture(
                cmd,
                sc.Window,
                out var texture,
                out var width,
                out var height
            )
        )
            throw new InvalidOperationException(
                $"SDL_WaitAndAcquireGPUSwapchainTexture failed: {SDL.GetError()}"
            );

        // 窗口最小化、尚未真正呈现等情况下 texture 会是 0 —— 这不是错误。
        // 此时必须放弃本帧：拿 0 去 BeginGPURenderPass 会在原生层直接段错误。
        //
        // 但它同时也是「窗口开着、画面不动、还不报错」最常见的元凶：
        // 一帧都拿不到纹理 → 本帧所有绘制被静默跳过。所以这里要告警一次。
        if (texture == IntPtr.Zero)
        {
            targetColor = IntPtr.Zero;
            targetDepth = IntPtr.Zero;
            acquiredSwapTarget = null;
            acquiredSwapTexture = IntPtr.Zero;
            acquiredSwapDepth = IntPtr.Zero;

            if (++emptyAcquireCount == 1)
                Log.Warning(
                    "[SDL3] SDL_WaitAndAcquireGPUSwapchainTexture returned a null texture; "
                        + "frames will be dropped silently. "
                        + $"driver={SDL.GetCurrentVideoDriver()} "
                        + $"windowFlags=0x{(ulong)SDL.GetWindowFlags(sc.Window):X} "
                        + $"logical={LogicalSizeText(sc.Window)} "
                        + $"pixel={PixelSizeText(sc.Window)}"
                );
            return;
        }

        if (emptyAcquireCount > 0)
            Log.Info($"[SDL3] swapchain texture acquired after {emptyAcquireCount} empty frame(s).");
        emptyAcquireCount = 0;

        acquiredSwapTarget = sc;
        acquiredSwapTexture = texture;
        sc.ApplyAcquiredSize(width, height);

        targetColor = texture;
        // 深度纹理的尺寸必须与本次 acquire 到的颜色纹理完全一致，
        // 所以只有拿到真实尺寸之后才能确认 / 重建它。
        acquiredSwapDepth = sc.EnsureDepthTexture(width, height);
        targetDepth = acquiredSwapDepth;
    }

    public void ClearColorTarget(uint index, RgbaFloat color)
    {
        if (renderPass != IntPtr.Zero)
            // 通道已开启后 SDL3 无法再清屏。按正确顺序调用（先 Clear 再 SetPipeline）
            // 不会走到这里，因此静默忽略即可。
            return;

        pendingClear = (index, color);
    }

    public void ClearDepthTarget(float depth = 1f)
    {
        if (renderPass != IntPtr.Zero)
            // 同 ClearColorTarget：SDL3 的清屏是通道启动参数。
            return;

        pendingDepthClear = depth;
    }

    public void SetPipeline(IPipeline pipeline)
    {
        if (pipeline is not SDL3Pipeline pl)
            throw new ArgumentException(
                $"Pipeline {pipeline?.GetType().Name ?? "null"} is not a SDL3Pipeline.",
                nameof(pipeline)
            );

        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        SDL.BindGPUGraphicsPipeline(renderPass, pl.Handle);

        // viewport / scissor 保持 SDL3 默认值（= 整个 color target）。
        //
        // y 轴翻转已在顶点着色器编译期完成（见 SDL3ResourceFactory.InjectFlipY），
        // 不在这里处理。两种备选方案都实测不可行：
        //   - 负高度 viewport：SDL3 后端会规范化掉，负值不生效；
        //   - FRAMEWORK_FLIP_Y 宏：Veldrid.SPIRV 的宏注入未生效。
        //
        // 另注：SDL_SetGPUViewport / SetGPUScissor 的第一个参数是
        // **SDL_GPURenderPass***，不是 SDL_GPUCommandBuffer*，传错会 abort。
    }

    public void SetVertexBuffer(uint slot, IDeviceBuffer buffer)
    {
        if (buffer is not SDL3Buffer bf)
            throw new ArgumentException(
                $"Buffer {buffer?.GetType().Name ?? "null"} is not a SDL3Buffer.",
                nameof(buffer)
            );

        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        GPUBufferBinding[] bindings = [new GPUBufferBinding { Buffer = bf.handle, Offset = 0 }];
        SDL.BindGPUVertexBuffers(renderPass, slot, bindings, 1);
    }

    public void SetIndexBuffer(IDeviceBuffer buffer, IndexFormat format, int offset = 0)
    {
        if (buffer is not SDL3Buffer bf)
            throw new ArgumentException(
                $"Index buffer {buffer?.GetType().Name ?? "null"} is not a SDL3Buffer.",
                nameof(buffer)
            );

        // SDL3 对漏声明 BufferUsage.Index 的缓冲不报错，
        // 表现为索引全读成 0 / 绘制结果空白。这类静默失败在这里先拦住。
        if ((bf.Usage & BufferUsage.Index) == 0)
            throw new ArgumentException(
                $"Buffer was created without BufferUsage.Index (actual: {bf.Usage}). "
                    + "Index buffers must declare it at creation time.",
                nameof(buffer)
            );

        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        // 注意与顶点缓冲的差异：
        //   SDL_BindGPUVertexBuffers 收 (pass, slot, 数组, 数量)
        //   SDL_BindGPUIndexBuffer   收 (pass, 单个绑定, 元素宽度)
        // 第二个参数在 C 侧是 const SDL_GPUBufferBinding*，绑定层生成为 in 参数
        // （同 SDL_SetGPUScissor 的 rect），因此直接传变量即可，无需 ref/in 关键字。
        var binding = new GPUBufferBinding { Buffer = bf.handle, Offset = (uint)offset };
        SDL.BindGPUIndexBuffer(
            renderPass,
            binding,
            format == IndexFormat.UInt16
                ? GPUIndexElementSize.IndexElementSize16Bit
                : GPUIndexElementSize.IndexElementSize32Bit
        );
    }

    public void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet)
    {
        if (resourceSet is not SDL3ResourceSet rs)
            throw new ArgumentException(
                $"Resource set {resourceSet?.GetType().Name ?? "null"} is not a SDL3ResourceSet.",
                nameof(resourceSet)
            );

        if (!recording || cmd == IntPtr.Zero)
            throw new InvalidOperationException(
                "SetGraphicsResourceSet must be called after Working = true."
            );

        // uniform 在 SDL3 里没有「绑定」这回事：
        // SDL_PushGPU{Vertex,Fragment}UniformData 在记录期就把数据拷贝进
        // 命令缓冲内部，之后所有绘制读的都是这份快照。
        //   → 改了 SDL3UniformBuffer 的内容而不重新 SetGraphicsResourceSet，
        //     本帧后续绘制仍然用旧数据。
        //
        // 它只需要命令缓冲有效，**不需要**渲染通道，也**不需要**交换链纹理。
        // 因此这里在一切之前先推：调用方把 SetGraphicsResourceSet 放在
        // SetRenderTarget / SetPipeline 之前或之后都能工作。
        uint vslot = slot;
        foreach (var u in rs.VertexUniforms)
            SDL.PushGPUVertexUniformData(cmd, vslot++, u.Bytes, (uint)u.SizeInBytes);

        uint fslot = slot;
        foreach (var u in rs.FragmentUniforms)
            SDL.PushGPUFragmentUniformData(cmd, fslot++, u.Bytes, (uint)u.SizeInBytes);

        // 采样器绑定必须发生在渲染通道内（SDL_BindGPU*Samplers 的第一个参数是
        // render pass）。而调用方可能在通道开启之前就绑定资源集，
        // 所以这里只登记，等通道真正开启时再应用 —— 见 ApplyPendingSamplers。
        pendingSet = rs;
        pendingSlot = slot;

        // 若通道已经开着（调用方在 SetPipeline 之后绑定），立刻补上采样器；
        // 否则留到 EnsurePass 成功时由它调用 ApplyPendingSamplers。
        ApplyPendingSamplers();
    }

    /// <summary>
    /// 把最近一次 <see cref="SetGraphicsResourceSet"/> 登记的采样器绑定应用到
    /// 当前渲染通道。通道尚未开启时是空操作，等 <see cref="EnsurePass"/> 之后重试。
    /// </summary>
    private void ApplyPendingSamplers()
    {
        if (pendingSet is null || renderPass == IntPtr.Zero)
            return;

        var rs = pendingSet;
        var slot = pendingSlot;

        if (rs.VertexSamplers.Length > 0)
            SDL.BindGPUVertexSamplers(
                renderPass,
                slot,
                rs.VertexSamplers,
                (uint)rs.VertexSamplers.Length
            );
        if (rs.FragmentSamplers.Length > 0)
            SDL.BindGPUFragmentSamplers(
                renderPass,
                slot,
                rs.FragmentSamplers,
                (uint)rs.FragmentSamplers.Length
            );
    }

    public void SetViewport(
        float x,
        float y,
        float width,
        float height,
        float minDepth = 0f,
        float maxDepth = 1f
    )
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"Viewport size must be positive, got {width}x{height}."
            );

        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        var viewport = new GPUViewport
        {
            X = x,
            Y = y,
            W = width,
            H = height,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
        };

        // 第一个参数是 render pass，不是 command buffer ——
        // 传错会让 SDL 断言 "Render pass not in progress!" 并直接 abort
        // （SIGSEGV / 退出码 134），比不设更危险。同 SetGPUScissor。
        SDL.SetGPUViewport(renderPass, viewport);
    }

    public void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
    {
        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        var rect = new Rect
        {
            X = (int)x,
            Y = (int)y,
            W = (int)width,
            H = (int)height,
        };
        // 第一个参数是 render pass，不是 command buffer —— 同 SetGPUViewport。
        SDL.SetGPUScissor(renderPass, rect);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart)
    {
        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;
        SDL.DrawGPUPrimitives(renderPass, vertexCount, instanceCount, vertexStart, instanceStart);
    }

    public void DrawIndexed(
        uint indexCount,
        uint instanceCount,
        uint indexStart,
        int vertexOffset,
        uint instanceStart
    )
    {
        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        // 原生签名：SDL_DrawGPUIndexedPrimitives(pass, num_indices, num_instances,
        //                                      first_index, vertex_offset, first_instance)
        // 与抽象层参数顺序逐位相同，注意第 5 个是 Int32（可为负），不要转成 uint。
        SDL.DrawGPUIndexedPrimitives(
            renderPass,
            indexCount,
            instanceCount,
            indexStart,
            vertexOffset,
            instanceStart
        );
    }

    // ---------------------------------------------------------------- 提交

    internal void Submit()
    {
        // 注意：不能因为 recording == false 就提前返回。
        // using (cl.Record()) 结束时 Dispose 已把 recording 置回 false，
        // 而真正的提交发生在那之后 —— 用 recording 判空会把正常路径也吞掉。
        //
        // 唯一的判据是 cmd 是否有效：BeginRecording 赋值、Submit 之后置零。
        EndPass();
        recording = false;

        if (cmd != IntPtr.Zero)
        {
            // SDL_SubmitGPUCommandBuffer 同时完成「提交 + 呈现」。
            // 返回值必须检查：它失败时画面会原样停住（不再呈现新帧），
            // 而没有异常、没有断言，是最容易误判成「渲染没生效」的一种失败。
            if (!SDL.SubmitGPUCommandBuffer(cmd) && !reportedSubmitFailure)
            {
                reportedSubmitFailure = true;
                Log.Error(
                    $"[SDL3] SDL_SubmitGPUCommandBuffer failed: {SDL.GetError()}. "
                        + "Frames will stop being presented."
                );
            }
            cmd = IntPtr.Zero;
        }

        targetColor = IntPtr.Zero;
        targetDepth = IntPtr.Zero;
        activeTarget = null;
        acquiredSwapTarget = null;
        acquiredSwapTexture = IntPtr.Zero;
        acquiredSwapDepth = IntPtr.Zero;
        pendingClear = null;
        pendingSet = null;
    }

    public void Dispose()
    {
        // 命令缓冲在提交时已被 SDL 回收；这里只清状态。
        cmd = IntPtr.Zero;
        renderPass = IntPtr.Zero;
        recording = false;
        targetColor = IntPtr.Zero;
        targetDepth = IntPtr.Zero;
        activeTarget = null;
        acquiredSwapTarget = null;
        acquiredSwapTexture = IntPtr.Zero;
        acquiredSwapDepth = IntPtr.Zero;
        pendingClear = null;
    }
}
