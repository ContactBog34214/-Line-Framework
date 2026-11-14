using Line.Framework.Graphics;
using Veldrid;

namespace Line.Framework.Default.Graphics.Veldrid;

/// <summary>
/// Veldrid 设备。
/// <para>
/// <b>为何创建设备时要同时给出交换链</b>：Veldrid 的 Vulkan 后端在创建
/// <c>VkInstance</c>/<c>VkDevice</c> 时需要 surface 才能判断哪些 queue family
/// 支持 present。若不提供，之后创建的交换链能构造成功却无法呈现
/// （窗口存在但内容永不更新）。
/// </para>
/// <para>
/// 因此推荐使用带 <see cref="SwapchainCreateInfo"/> 的构造器，
/// 设备与第一个交换链一起创建，交换链通过 <c>out</c> 返回。
/// 框架不区分「主」交换链 —— 返回的就是一个普通交换链，
/// 调用方负责它的生命周期；之后仍可用
/// <c>ResourceFactory.CreateSwapchain(...)</c> 创建更多交换链。
/// </para>
/// </summary>
public sealed class VeldridDevice : IGraphicsDevice
{
    private readonly GraphicsDevice graphicsDevice;

    public string Name => graphicsDevice.DeviceName;

    /// <summary>
    /// 创建设备，并同时创建第一个交换链。
    /// </summary>
    /// <param name="backend">图形后端。</param>
    /// <param name="swapchainCreateInfo">交换链参数（窗口句柄、尺寸等）。</param>
    /// <param name="swapchain">
    /// 创建好的交换链。由调用方负责 Dispose —— 设备不持有它的所有权。
    /// </param>
    public VeldridDevice(
        GraphicBackend backend,
        SwapchainCreateInfo swapchainCreateInfo,
        out ISwapchain swapchain
    )
    {
        var description = VeldridConverter.ConvertSwapchainDescription(swapchainCreateInfo);
        graphicsDevice = CreateDevice(backend, description);
        ResourceFactory = new VeldridResourceFactory(graphicsDevice);

        swapchain = new VeldridSwapchain(graphicsDevice.MainSwapchain, graphicsDevice);
    }

    /// <summary>
    /// 只创建设备，不创建交换链。
    /// <para>
    /// <b>Vulkan 下不推荐</b>：没有 surface 时无法确定 present queue family，
    /// 后续创建的交换链无法呈现。仅在无窗口场景（离屏渲染、测试）使用。
    /// </para>
    /// </summary>
    public VeldridDevice(GraphicBackend backend)
    {
        graphicsDevice = CreateDevice(backend, null);
        ResourceFactory = new VeldridResourceFactory(graphicsDevice);
    }

    public IResourceFactory ResourceFactory { get; }

    /// <summary>
    /// 创建设备。<paramref name="description"/> 非空时一并创建 surface，
    /// 并选择同时支持 graphics 与 present 的 queue family。
    /// </summary>
    private static GraphicsDevice CreateDevice(
        GraphicBackend backend,
        SwapchainDescription? description
    )
    {
        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            SyncToVerticalBlank = false,
            ResourceBindingModel = ResourceBindingModel.Default,
        };

        switch (backend)
        {
            case GraphicBackend.Vulkan:
                // 实例必须预先请求 surface 扩展，否则 vkCreateXlibSurfaceKHR
                // 等调用会因实例未启用对应扩展而失败。
                var vkOptions = new VulkanDeviceOptions(
                    GetVulkanInstanceExtensions(),
                    []
                );
                return description is { } vkDesc
                    ? GraphicsDevice.CreateVulkan(options, vkDesc, vkOptions)
                    : GraphicsDevice.CreateVulkan(options, vkOptions);

            case GraphicBackend.Metal:
                return description is { } mtlDesc
                    ? GraphicsDevice.CreateMetal(options, mtlDesc)
                    : GraphicsDevice.CreateMetal(options);

            case GraphicBackend.Direct3D:
                return description is { } d3dDesc
                    ? GraphicsDevice.CreateD3D11(options, d3dDesc)
                    : GraphicsDevice.CreateD3D11(options);

            case GraphicBackend.OpenGL:
                throw new NotSupportedException("OpenGL backend is not supported.");

            default:
                throw new NotSupportedException($"Unsupported backend {backend}.");
        }
    }

    /// <summary>
    /// 按平台列出 Vulkan 实例需要的 surface 扩展。
    /// 未安装的扩展由 loader 忽略，不影响其他后端。
    /// </summary>
    private static string[] GetVulkanInstanceExtensions()
    {
        if (OperatingSystem.IsWindows())
            return ["VK_KHR_surface", "VK_KHR_win32_surface"];

        if (OperatingSystem.IsMacOS())
            return ["VK_KHR_surface", "VK_EXT_metal_surface"];

        // Linux：X11 与 Wayland 都请求，实际生效哪个由窗口系统决定
        return ["VK_KHR_surface", "VK_KHR_xlib_surface", "VK_KHR_wayland_surface"];
    }

    public void Dispose()
    {
        // 先释放工厂持有的常驻资源（默认采样器等），再释放设备。
        // 交换链由调用方持有，此处不释放。
        (ResourceFactory as VeldridResourceFactory)?.DisposeOwned();
        graphicsDevice?.Dispose();
    }

    public void WaitForIdle()
    {
        graphicsDevice?.WaitForIdle();
    }

    public void SubmitCommands(ICommandList commandList)
    {
        if (commandList is not VeldridCommandList)
            throw new InvalidDataException();
        graphicsDevice?.SubmitCommands((commandList as VeldridCommandList).cl);
    }
}
