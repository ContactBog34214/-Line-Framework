using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Line.Framework.Graphics;

namespace Line.Framework.Resource.Graphic;

public class RResourceSet : IResource
{
    public bool IsLoaded
    {
        get { return resourceSet != null; }
    }
    IResourceSet resourceSet;
    TResourceSet parent;
    ITexture t;

    /// <summary>
    /// 串行化 <see cref="Load"/> 与 <see cref="Release"/> 的门。
    /// <para>
    /// <b>为什么必需</b>：<see cref="RResourceSet"/> 的 GPU 资源会被多处访问 ——
    /// <see cref="EnableMipmap"/> 的重载在输入回调线程，
    /// 而渲染线程同时在调 <c>GetResource</c>（<see cref="IsLoaded"/> 为 false
    /// 时也会进 <see cref="Load"/>）。交错执行时一个线程刚建好的 <c>t</c>
    /// 会被另一个线程清理旧纹理的逻辑置空，接着 NRE；
    /// 更糟的是双方同时触碰同一张纹理，在 SDL3 上直接段错误（退出码 139）。
    /// </para>
    /// <para>
    /// 两个入口内部都<b>没有 await</b>（见各自说明），所以用 <c>Wait()</c>
    /// 而非 <c>WaitAsync()</c> —— 保证从进入到返回全程在同一个线程。
    /// </para>
    /// </summary>
    private readonly SemaphoreSlim gate = new(1, 1);

    public object GetHandle()
    {
        return new ResourceSetArg(t, resourceSet);
    }

    /// <summary>
    /// 加载（或重载）纹理。
    /// <para>
    /// <b>故意做成同步</b>：内部没有任何 <c>await</c>，因此从调用到返回
    /// 始终在同一个线程上完成。这对 SDL3 是硬性要求 —— 它的 GPU API
    /// 只能由创建 device 的那个线程调用，一旦 <c>await</c> 把续体
    /// 调度到线程池，<c>SDL_CreateGPUTexture</c> 就会让进程段错误
    /// （退出码 139，C# 层看不到任何异常）。
    /// </para>
    /// <para>
    /// 数据源是 <see cref="MemoryStream"/>，同步读不会阻塞 IO，
    /// 代价只有 CPU 降采样 + 一次纹理上传，可接受。
    /// </para>
    /// </summary>
    public Task Load()
    {
        gate.Wait();
        try
        {
            LoadCore();
        }
        finally
        {
            gate.Release();
        }

        return Task.CompletedTask;
    }

    private void LoadCore()
    {
        if (ImgStream == null)
            return;
        if (resourceSet != null)
            return;
        if (t != null)
            try
            {
                // 旧纹理可能仍在在途命令里，交给设备的延迟释放。
                var dev = parent?.Dev;
                if (dev is null)
                    t.Dispose();
                else
                    dev.DisposeWhenIdle(t);
            }
            catch (Exception ex)
            {
                Log.Warning($"Cannot Dispose Texture:{ex}");
            }
            finally
            {
                t = null;
            }

        //CreateTexture
        try
        {
            ushort metaDataLength = 0;
            ImgStream.Position = 0;
            byte[] ml = new byte[2];
            ImgStream.ReadExactly(ml, 0, 2);
            metaDataLength = BinaryPrimitives.ReadUInt16LittleEndian(ml);

            byte[] dataBytes = new byte[metaDataLength];
            ImgStream.Position = 2;
            ImgStream.ReadExactly(dataBytes);

            if (!MemoryMarshal.TryRead(dataBytes, out TextureResourceStreamInfo metaData))
                throw new InvalidDataException(
                    $"meta truncated: {metaDataLength} < ${Marshal.SizeOf<TextureResourceStreamInfo>()}"
                );
            ImgStream.Position = 2 + metaDataLength;

            t = null;

            // ---- mip 层数决策 ------------------------------------------
            // 关键：申请了几层就必须填满几层。只上传第 0 层会让
            // mip1 及以下是未初始化显存，采样器落到那几层就是
            // 「贴图发淡 / 随旋转闪烁 / 小图整块消失」。
            //
            // 因此这里先决定「能不能生成」，再决定「申请几层」——
            // 不能生成（格式不支持）时就只申请 1 层，绝不留空洞。
            var format = (PixelFormat)metaData.Format;
            int bytesPerPixel;
            try
            {
                bytesPerPixel = format.GetBytesPerPixel();
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidDataException(
                    $"Texture format {format} has no known byte size: {ex.Message}",
                    ex
                );
            }

            uint maxLevels = MipmapGenerator.GetMipLevelCount(metaData.Width, metaData.Height);

            // 三重要求全部满足才生成：
            //   1) 用户开了 EnableMipmap
            //   2) 后端支持 mipmap 采样（Veldrid 为 false —— 生成也用不上，白耗 CPU 与显存）
            //   3) 格式能正确降采样、尺寸够大
            bool canGenerateMip =
                EnableMipmap
                && parent.Dev.Capabilities.SupportsMipmap
                && maxLevels > 1
                && MipmapGenerator.IsSupported(format);

            uint mipLevels = canGenerateMip
                ? (
                    DefaultMipLevels == UnlimitedMipLevels
                        ? maxLevels
                        : Math.Min(DefaultMipLevels, maxLevels)
                )
                : 1u;

            t = parent.Dev.ResourceFactory.CreateTexture(
                new()
                {
                    ArrayLayers = metaData.ArrayLayers,
                    Format = format,
                    Width = metaData.Width,
                    Height = metaData.Height,
                    MipLevels = mipLevels,
                    Usage = TextureUsage.Sampled,
                }
            );

            long totLength = ImgStream.Length - ImgStream.Position;

            long bytesPerLayer = totLength / metaData.ArrayLayers;

            for (uint arrayLayer = 0; arrayLayer < metaData.ArrayLayers; arrayLayer++)
            {
                byte[] layerBytes = new byte[bytesPerLayer];
                ImgStream.ReadExactly(layerBytes);

                t.Update(layerBytes, arrayLayer: arrayLayer);

                // 逐级生成并上传 mip 1..N。
                // 每层独立生成（不同数组层的内容不同），不能复用上一层的链。
                if (mipLevels > 1)
                {
                    uint capturedLayer = arrayLayer;
                    MipmapGenerator.GenerateAndUpload(
                        layerBytes,
                        metaData.Width,
                        metaData.Height,
                        bytesPerPixel,
                        mipLevels,
                        // 必须显式传该层的宽高：ITexture.Update 的默认值
                        // 是「纹理完整尺寸」(mip0 的 Width/Height)，
                        // 而 mip N 的数据只有 Width>>N 那么大。
                        // 不传就会被长度校验拒绝（例如 400x400 的 mip1
                        // 只有 160000 字节，却按 640000 校验）。
                        (data, level, w, h) =>
                            t.Update(
                                data,
                                x: 0,
                                y: 0,
                                z: 0,
                                width: (int)w,
                                height: (int)h,
                                depth: null,
                                mipLevel: level,
                                arrayLayer: capturedLayer
                            )
                    );
                }
            }
        }
        catch
        {
            // 加载失败：这张纹理是本方法刚建的，还没交给任何命令列表，
            // 可以直接释放。
            t?.Dispose();
            t = null;
            throw;
        }
        if (t == null)
            return;
        resourceSet = parent.Dev.ResourceFactory.CreateResourceSet(
            new() { Layout = parent.Layout, Texture = t }
        );
    }

    /// <summary>
    /// 释放纹理与资源集（同步完成）。
    /// <para>
    /// 内部<b>没有 await</b>：<see cref="EnableMipmap"/> 会同步调用本方法，
    /// 若这里有 await 就可能把续体调度到别的线程，进而让随后的
    /// <see cref="Load"/> 在错误线程上触碰 GPU 资源。
    /// </para>
    /// </summary>
    public Task Release()
    {
        gate.Wait();
        try
        {
            // 交给设备的延迟释放：资源可能仍被在途命令引用。
            // SDL3 的 SDL_ReleaseGPU* 本身延迟回收；Veldrid 入队等设备空闲。
            var dev = parent?.Dev;
            if (dev is null)
            {
                resourceSet?.Dispose();
                t?.Dispose();
            }
            else
            {
                if (resourceSet is not null)
                    dev.DisposeWhenIdle(resourceSet);
                if (t is not null)
                    dev.DisposeWhenIdle(t);
            }

            resourceSet = null;
            t = null;
        }
        finally
        {
            gate.Release();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        ImgStream?.Dispose();
        // Release 内部已释放纹理与资源集，这里不再重复。
        Release().GetAwaiter().GetResult();
    }

    public RResourceSet(Stream stream, TResourceSet tr)
    {
        parent = tr;
        if (stream.CanSeek)
            stream.Position = 0;
        ImgStream = new MemoryStream();
        stream.CopyTo(ImgStream);
    }

    private readonly Stream ImgStream;

    /// <summary>
    /// 启用 mipmap 时生成的层数上限。<see cref="UnlimitedMipLevels"/> 表示用满。
    /// <para>
    /// 实际是否生成还要看后端能力
    /// （<see cref="IGraphicsCapabilities.SupportsMipmap"/>）、格式与尺寸 ——
    /// 任一不满足就只申请 1 层。
    /// </para>
    /// <para>
    /// 用满（而不是固定 4 层）是必要的：mip 层数不够深时，缩小到很小尺寸
    /// 的纹理仍然只能采到「不够小的 mip」，锯齿和闪烁照样存在。
    /// 例如 400×400 的图缩到 40 像素显示，需要的层数是 log2(10) ≈ 3.32，
    /// 而 4 层（最深 50×50）刚好不够。
    /// </para>
    /// <para>
    /// 内存代价很小：mip 链总和 = 4/3 × 基础层，即多 33%，
    /// 换来的是任意缩小倍率下都有合适的 mip 可用。
    /// </para>
    /// </summary>
    public const uint DefaultMipLevels = UnlimitedMipLevels;

    /// <summary>
    /// 「用满」哨兵值：实际层数取尺寸允许的上限
    /// （<see cref="MipmapGenerator.GetMipLevelCount"/>）。
    /// </summary>
    public const uint UnlimitedMipLevels = uint.MaxValue;

    /// <summary>
    /// 是否生成 mip 链。修改后立即重载（同步完成，不跨线程）。
    /// <para>
    /// 后端不支持 mipmap（<see cref="IGraphicsCapabilities.SupportsMipmap"/>
    /// 为 false，如 Veldrid）时，设为 true 也不会真正生成 —— 由加载逻辑过滤。
    /// </para>
    /// </summary>
    public bool EnableMipmap
    {
        get;
        set
        {
            if (value == field)
                return;
            field = value;

            var wasLoaded = IsLoaded;
            Release().GetAwaiter().GetResult();
            if (wasLoaded)
                Load().GetAwaiter().GetResult();
        }
    } = true;
}

public class TResourceSet : ResourceType
{
    internal IGraphicsDevice Dev { get; init; }
    internal IResourceLayout Layout { get; init; }

    public TResourceSet(IGraphicsDevice dev, IResourceLayout layout)
    {
        Dev = dev;
        Layout = layout;
    }

    public override async Task<IResource> Create(Stream stream)
    {
        return new RResourceSet(stream, this);
    }
}

public class ResourceSetArg
{
    public ITexture Texture { get; init; }
    public IResourceSet ResourceSet { get; init; }

    internal ResourceSetArg(ITexture t, IResourceSet rs)
    {
        Texture = t;
        ResourceSet = rs;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct TextureResourceStreamInfo
{
    [FieldOffset(0)]
    public uint Width = 0;

    [FieldOffset(4)]
    public uint Height = 0;

    [FieldOffset(8)]
    public ushort Format = (ushort)PixelFormat.R8G8B8A8UNorm;

    [FieldOffset(12)]
    public uint ArrayLayers = 1;

    public TextureResourceStreamInfo(uint width, uint height, PixelFormat format, uint arrayLayers)
    {
        Width = width;
        Height = height;
        Format = (ushort)format;
        ArrayLayers = arrayLayers;
    }
}
