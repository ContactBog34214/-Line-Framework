using System.Text;
using Line.Framework;
using Line.Framework.Default.Graphics.Veldrid;
using Line.Framework.Graphics;
using SDL3;

namespace LineFramework.GraphicsTest;

public static class GT
{
    public const string VertexCode =
        @"
#version 450
layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}";

    public const string FragmentCode =
        @"
#version 450
layout(location = 0) in vec4 fsin_Color;

layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

    public static async Task Main()
    {
        var dev = new VeldridDevice(GraphicBackend.Vulkan);

        var rl = dev.ResourceFactory.CreateResourceLayout(
            new()
            {
                Elements =
                [
                    new()
                    {
                        Kind = ResourceKind.StorageTexture,
                        Name = "_texture",
                        Stages = ShaderStage.Fragment,
                    },
                ],
            }
        );

        var vl = new VertexLayoutDescription()
        {
            Stride = 24,
            Attributes =
            [
                new() { Format = VertexFormat.Float2, Offset = 0 },
                new() { Format = VertexFormat.Float4, Offset = 8 },
            ],
        };

        var fs = dev.ResourceFactory.CreateShader(
            new()
            {
                Bytes = Encoding.UTF8.GetBytes(FragmentCode),
                EntryPoint = "main",
                Stage = ShaderStage.Fragment,
            }
        );
        var vs = dev.ResourceFactory.CreateShader(
            new()
            {
                Bytes = Encoding.UTF8.GetBytes(VertexCode),
                EntryPoint = "main",
                Stage = ShaderStage.Vertex,
            }
        );

        var texture = dev.ResourceFactory.CreateTexture(
            new()
            {
                Width = 640,
                Height = 480,
                Format = PixelFormat.R8G8B8A8UNorm,
                //Type = TextureType.Texture2D,
                SampleCount = SampleCount.Count1,
                Usage = TextureUsage.Sampled,
            }
        );
        var view = dev.ResourceFactory.CreateTextureView(
            new()
            {
                TargetTexture = texture,
                BaseArrayLayer = 0,
                ArrayLayers = texture.ArrayLayers,
                BaseMipLevel = 0,
                MipLevels = texture.MipLevels,
            }
        );
        var output = dev.ResourceFactory.CreateFrameBuffer(
            new() { ColorTargets = [new() { Target = view }] }
        );

        GraphicsDescription gd = new()
        {
            RasterizerState = new()
            {
                CullMode = FaceCullMode.None,
                FillMode = PolygonFillMode.Solid,
                FrontFace = FrontFace.Clockwise,
                DepthClipEnabled = true,
                ScissorTestEnabled = true,
            },
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            ShaderSet = new() { Shaders = [vs, fs], VertexLayouts = [vl] },
            BlendState = BlendStateDescription.SingleAlphaBlend,
            Outputs = output.OutputFormat,
            DepthStencilState = DepthStencilStateDescription.Disabled,
        };

        var pl = dev.ResourceFactory.CreatePipeline(new() { Mode = gd, ResourceLayouts = [rl] });

        if (
            OperatingSystem.IsLinux()
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
        )
            SDL.SetHint(SDL.Hints.VideoDriver, "wayland");

        SDL.Init(SDL.InitFlags.Video);

        IntPtr window = SDL.CreateWindow("GraphicsTest", 1280, 720, SDL.WindowFlags.Vulkan);
        var driver = SDL.GetCurrentVideoDriver();
        SwapchainSource source = default;

        uint props = SDL.GetWindowProperties(window);
        if (driver == "wayland")
        {
            source = new()
            {
                Kind = NativeWindowKind.Wayland,
                Surface = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandSurfacePointer,
                    IntPtr.Zero
                ),
                Connection = SDL.GetPointerProperty(
                    props,
                    SDL.Props.WindowWaylandDisplayPointer,
                    IntPtr.Zero
                ),
            };
        }

        var chain = dev.ResourceFactory.CreateSwapchain(
            new()
            {
                Source = source,
                Width = 1280,
                Height = 720,
                SyncToVerticalBlank = false,
            }
        );

        using (var cl = dev.ResourceFactory.CreateCommandList())
        {
            using (cl.Record())
            {
                cl.SetFrameBuffer(chain.FrameBuffer);
                cl.SetPipeline(pl);
                cl.ClearColorTarget(0, new(255, 0, 0, 255));
            }
            dev.SubmitCommands(cl);
        }
        dev.WaitForIdle();
        chain.Swap();

        Console.WriteLine(dev.Name);
        Console.WriteLine(pl.Outputs);
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
