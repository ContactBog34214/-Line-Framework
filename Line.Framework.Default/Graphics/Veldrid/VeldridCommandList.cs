using Line.Framework.Graphics;
using Line.Framework.Types;

namespace Line.Framework.Default.Graphics.Veldrid
{
    public class VeldridCommandList : ICommandList
    {
        internal VeldridCommandList(global::Veldrid.CommandList commandList)
        {
            cl = commandList;
        }

        internal global::Veldrid.CommandList cl { get; }
        private readonly Lock workingStateLock = new();

        /// <summary>当前活跃的管线；null 表示还没设过（Veldrid 此时不允许绑资源集）。</summary>
        private VeldridPipeline pendingPipeline;

        /// <summary>
        /// 在管线就位之前登记的资源集，等 SetPipeline 后按顺序补绑。
        /// 用列表而非单值：允许一次录制里绑多个槽位。
        /// </summary>
        private readonly List<(uint Slot, VeldridResourceSet Set)> pendingSets = [];

        /// <summary>
        /// 当前渲染目标是否有深度附件。在 <see cref="SetRenderTarget"/> 时记录一次，
        /// 供 <see cref="ClearDepthTarget"/> 判断是否该真正下清屏命令。
        /// </summary>
        private bool depthTargetAvailable;

        public bool Working
        {
            get;
            set
            {
                lock (workingStateLock)
                {
                    if (value == field)
                        if (value)
                            throw new InvalidOperationException(
                                $"Command state already is {field}"
                            );
                        else
                            return;
                    if (value)
                    {
                        cl.Begin();
                        // 新一次录制从「没有活跃管线」开始，
                        // 上一次残留的登记不能带过来。
                        pendingPipeline = null;
                        pendingSets.Clear();
                        depthTargetAvailable = false;
                    }
                    else
                        cl.End();
                    field = value;
                }
            }
        } = false;

        public string Name => cl?.Name;

        public void ClearColorTarget(uint index, RgbaFloat color)
        {
            cl.ClearColorTarget(index, VeldridConverter.ConvertRgbaFloat(color));
        }

        public void ClearDepthTarget(float depth = 1f)
        {
            // Veldrid 的 ClearDepthStencil 不检查目标有没有深度附件：
            // 当前 framebuffer 没有深度时，驱动侧是空操作或验证错误。
            // 抽象层承诺「没有深度附件时是空操作」，所以这里显式拦一下。
            if (!depthTargetAvailable)
                return;

            // 用两参重载：两个可用深度格式都带 8 位模板，
            // 顺手把模板也清成 0，避免残留值影响将来接入模板的管线。
            cl.ClearDepthStencil(depth, 0);
        }

        public void Dispose()
        {
            if (Working)
                Working = false;
            cl?.Dispose();
        }

        public void Draw(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart)
        {
            cl.Draw(vertexCount, instanceCount, vertexStart, instanceStart);
        }

        public void DrawIndexed(
            uint indexCount,
            uint instanceCount,
            uint indexStart,
            int vertexOffset,
            uint instanceStart
        )
        {
            // 参数顺序与 Veldrid 原生签名逐位相同，直传即可。
            cl.DrawIndexed(indexCount, instanceCount, indexStart, vertexOffset, instanceStart);
        }

        public IDisposable Record()
        {
            if (Working)
                throw new InvalidOperationException("Already recording");
            Working = true;
            return new _RecordScope(this);
        }

        private class _RecordScope(VeldridCommandList t) : IDisposable
        {
            private bool released = false;

            public void Dispose()
            {
                if (released)
                    return;
                released = true;
                t?.Working = false;
            }
        }

        public void SetRenderTarget(IRenderTarget renderTarget)
        {
            if (renderTarget is not VeldridRenderTarget rt)
                throw new ArgumentException(
                    $"Render target {renderTarget?.GetType().Name ?? "null"} "
                        + "is not a VeldridRenderTarget.",
                    nameof(renderTarget)
                );
            cl.SetFramebuffer(rt.framebuffer);
            depthTargetAvailable = rt.DepthTexture is not null;
        }

        public void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet)
        {
            if (!(resourceSet is VeldridResourceSet rs))
                throw new InvalidDataException();

            // Veldrid 要求「绑定资源集前必须已有活跃管线」，
            // 否则直接抛 VeldridException（不像 SDL3 那样静默）。
            //
            // 但调用方可能为了照顾 SDL3 而把绑定放在 SetPipeline 之前
            //（SDL3 的 uniform 必须在渲染通道外推送）。
            // 两个后端的顺序要求互斥，所以这里统一做延迟：
            // 管线还没设就登记下来，等 SetPipeline 之后再应用。
            // 这样调用方写在哪边都能工作。
            if (pendingPipeline is null)
            {
                pendingSets.Add((slot, rs));
                return;
            }

            cl.SetGraphicsResourceSet(slot, rs.ResourceSet);
        }

        /// <summary>
        /// 把 <see cref="SetPipeline"/> 之前登记的资源集补绑上。
        /// </summary>
        private void ApplyPendingResourceSet()
        {
            if (pendingSets.Count == 0)
                return;

            foreach (var (slot, set) in pendingSets)
                cl.SetGraphicsResourceSet(slot, set.ResourceSet);

            pendingSets.Clear();
        }

        public void SetPipeline(IPipeline pipeline)
        {
            if (!(pipeline is VeldridPipeline pl))
                throw new InvalidDataException();
            cl.SetPipeline(pl.Pipeline);
            pendingPipeline = pl;

            // 管线就位后，补绑之前登记的资源集。
            ApplyPendingResourceSet();
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

            // Veldrid 的 SetViewport 带 index（Vulkan 支持多视口），
            // 抽象层只暴露单视口，所以固定用 0。
            cl.SetViewport(0, new global::Veldrid.Viewport(x, y, width, height, minDepth, maxDepth));
        }

        public void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
        {
            cl.SetScissorRect(index, x, y, width, height);
        }

        public void SetVertexBuffer(uint slot, IDeviceBuffer buffer)
        {
            if (!(buffer is VeldridBuffer bf))
                throw new InvalidDataException();
            cl.SetVertexBuffer(slot, bf.buffer);
        }

        public void SetIndexBuffer(IDeviceBuffer buffer, IndexFormat format, int offset = 0)
        {
            if (!(buffer is VeldridBuffer bf))
                throw new InvalidDataException(
                    $"Index buffer {buffer?.GetType().Name ?? "null"} is not a VeldridBuffer."
                );

            // Vulkan 绑错用途的缓冲不是「报个错」那么温和：可能是验证层报错后
            // 继续执行、直接 device lost，或者静默读到垃圾。这里提前拦住。
            if ((bf.Usage & BufferUsage.Index) == 0)
                throw new ArgumentException(
                    $"Buffer was created without BufferUsage.Index (actual: {bf.Usage}). "
                        + "Index buffers must declare it at creation time.",
                    nameof(buffer)
                );

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            cl.SetIndexBuffer(
                bf.buffer,
                format == IndexFormat.UInt16
                    ? global::Veldrid.IndexFormat.UInt16
                    : global::Veldrid.IndexFormat.UInt32,
                (uint)offset
            );
        }

    }

    public partial class VeldridResourceFactory
    {
        public ICommandList CreateCommandList(string Name = null)
        {
            var cl = dev.ResourceFactory.CreateCommandList();
            cl.Name = Name;
            return new VeldridCommandList(cl);
        }
    }
}
