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

    /// <summary>当前绑定的渲染目标（同一时间只支持一个）。</summary>
    private SDL3FrameBuffer target;

    /// <summary>尚未被渲染通道消费的清屏颜色。</summary>
    private (uint Index, RgbaFloat Color)? pendingClear;

    private bool recording;

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
        target = null;
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

        // 没有有效目标（未调用 SetFrameBuffer，或交换链纹理本帧不可用）：
        // 不建通道，后续所有绑定/绘制由各自的 renderPass 判空跳过。
        if (target is null || target.AcquiredTexture == IntPtr.Zero)
            return;

        var info = new GPUColorTargetInfo
        {
            Texture = target.AcquiredTexture,
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
        renderPass = SDL.BeginGPURenderPass(cmd, targets, 1, IntPtr.Zero);
        pendingClear = null;

        if (renderPass == IntPtr.Zero)
            return;

        // 不在这里设 viewport / scissor。
        //
        // SDL3 的渲染通道默认视口 = 整个 color target，默认 scissor = 全域，
        // 因此全屏绘制（UI 合成器的情形）无需手动设置。
        //
        // 且 SDL_SetGPUViewport / SDL_SetGPUScissor 内部会断言
        // "Render pass not in progress!"，一旦此时通道状态与 SDL 内部不一致，
        // 断言直接 abort（退出码 134），比不设更危险。
        //
        // 需要局部裁剪时走 SetScissorRect，届时再补 viewport 逻辑。
    }

    // ---------------------------------------------------------------- 状态绑定

    public void SetFrameBuffer(IFrameBuffer frameBuffer)
    {
        if (frameBuffer is not SDL3FrameBuffer fb)
            throw new ArgumentException(
                $"Framebuffer {frameBuffer?.GetType().Name ?? "null"} is not a SDL3FrameBuffer.",
                nameof(frameBuffer)
            );

        if (!recording)
            throw new InvalidOperationException(
                "SetFrameBuffer must be called after Working = true."
            );

        // 换目标必须先结束当前通道
        EndPass();

        // SDL3 的交换链目标是在命令缓冲上「取」出来的，
        // 且只能由取它的那个命令缓冲引用。
        if (
            !SDL.WaitAndAcquireGPUSwapchainTexture(
                cmd,
                fb.Window,
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
        if (texture == IntPtr.Zero)
        {
            target = null;
            return;
        }

        fb.AcquiredTexture = texture;
        fb.AcquiredWidth = width;
        fb.AcquiredHeight = height;
        fb.ApplyAcquiredSize();

        target = fb;
    }

    public void ClearColorTarget(uint index, RgbaFloat color)
    {
        if (renderPass != IntPtr.Zero)
            // 通道已开启后 SDL3 无法再清屏。按正确顺序调用（先 Clear 再 SetPipeline）
            // 不会走到这里，因此静默忽略即可。
            return;

        pendingClear = (index, color);
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

    public void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet)
    {
        if (resourceSet is not SDL3ResourceSet rs)
            throw new ArgumentException(
                $"Resource set {resourceSet?.GetType().Name ?? "null"} is not a SDL3ResourceSet.",
                nameof(resourceSet)
            );

        EnsurePass();
        if (renderPass == IntPtr.Zero)
            return;

        // SDL3 没有 descriptor set：纹理与采样器成对直接绑到通道上。
        // 抽象层的 TextureSampler 正好一个对应一个绑定。
        if (rs.Bindings.Length > 0)
            SDL.BindGPUFragmentSamplers(renderPass, slot, rs.Bindings, (uint)rs.Bindings.Length);
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
            SDL.SubmitGPUCommandBuffer(cmd);
            cmd = IntPtr.Zero;
        }

        target = null;
        pendingClear = null;
    }

    public void Dispose()
    {
        // 命令缓冲在提交时已被 SDL 回收；这里只清状态。
        cmd = IntPtr.Zero;
        renderPass = IntPtr.Zero;
        recording = false;
        target = null;
        pendingClear = null;
    }
}
