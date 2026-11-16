using Line.Framework.Graphics;
using Line.Framework.Types;
using SDL3;
// SDL3-CS 把全部 GPU 相关类型作为嵌套类型放在静态类 SDL3.SDL 里，
// `using SDL3;` 只能解析到 SDL 这个类本身。
// `using static` 会连同嵌套类型一起导入，这样 GPUTextureFormat 等才能裸用。
using static SDL3.SDL;
using BlendFactor = Line.Framework.Graphics.BlendFactor;
using BlendOperation = Line.Framework.Graphics.BlendOperation;
using PixelFormat = Line.Framework.Graphics.PixelFormat;

namespace Line.Framework.Default.Graphics.SDL3;

/// <summary>
/// 抽象层 → SDL3 GPU 的枚举与描述体转换。
/// <para>
/// 与 <c>VeldridConverter</c> 不同：SDL3 没有 ResourceLayout / ResourceSet /
/// SpecializationConstant 等概念，因此这里只做「枚举映射」与「描述体组装」。
/// </para>
/// </summary>
public static class SDL3Converter
{
    // ---------------------------------------------------------------- PixelFormat

    public static GPUTextureFormat ConvertPixelFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8UNorm => GPUTextureFormat.R8Unorm,
            PixelFormat.R8G8UNorm => GPUTextureFormat.R8G8Unorm,
            PixelFormat.R8G8B8A8UNorm => GPUTextureFormat.R8G8B8A8Unorm,
            PixelFormat.B8G8R8A8UNorm => GPUTextureFormat.B8G8R8A8Unorm,
            PixelFormat.R8SNorm => GPUTextureFormat.R8Snorm,
            PixelFormat.R8G8SNorm => GPUTextureFormat.R8G8Snorm,
            PixelFormat.R8G8B8A8SNorm => GPUTextureFormat.R8G8B8A8Snorm,
            PixelFormat.R16UNorm => GPUTextureFormat.R16Unorm,
            PixelFormat.R16G16UNorm => GPUTextureFormat.R16G16Unorm,
            PixelFormat.R16G16B16A16UNorm => GPUTextureFormat.R16G16B16A16Unorm,
            PixelFormat.R16Float => GPUTextureFormat.R16Float,
            PixelFormat.R16G16Float => GPUTextureFormat.R16G16Float,
            PixelFormat.R16G16B16A16Float => GPUTextureFormat.R16G16B16A16Float,
            PixelFormat.R32Float => GPUTextureFormat.R32Float,
            PixelFormat.R32G32Float => GPUTextureFormat.R32G32Float,
            PixelFormat.R32G32B32A32Float => GPUTextureFormat.R32G32B32A32Float,
            // SDL3 没有 8/16 位整型纹理格式（只有 unorm/snorm/float）
            _ => throw new NotSupportedException(
                $"PixelFormat {format} has no SDL3 GPU equivalent."
            ),
        };
    }

    public static PixelFormat ConvertPixelFormat(GPUTextureFormat format)
    {
        return format switch
        {
            GPUTextureFormat.R8Unorm => PixelFormat.R8UNorm,
            GPUTextureFormat.R8G8Unorm => PixelFormat.R8G8UNorm,
            GPUTextureFormat.R8G8B8A8Unorm => PixelFormat.R8G8B8A8UNorm,
            GPUTextureFormat.B8G8R8A8Unorm => PixelFormat.B8G8R8A8UNorm,
            GPUTextureFormat.R8Snorm => PixelFormat.R8SNorm,
            GPUTextureFormat.R8G8Snorm => PixelFormat.R8G8SNorm,
            GPUTextureFormat.R8G8B8A8Snorm => PixelFormat.R8G8B8A8SNorm,
            GPUTextureFormat.R16Unorm => PixelFormat.R16UNorm,
            GPUTextureFormat.R16G16Unorm => PixelFormat.R16G16UNorm,
            GPUTextureFormat.R16G16B16A16Unorm => PixelFormat.R16G16B16A16UNorm,
            GPUTextureFormat.R16Float => PixelFormat.R16Float,
            GPUTextureFormat.R16G16Float => PixelFormat.R16G16Float,
            GPUTextureFormat.R16G16B16A16Float => PixelFormat.R16G16B16A16Float,
            GPUTextureFormat.R32Float => PixelFormat.R32Float,
            GPUTextureFormat.R32G32Float => PixelFormat.R32G32Float,
            GPUTextureFormat.R32G32B32A32Float => PixelFormat.R32G32B32A32Float,
            _ => throw new NotSupportedException(
                $"GPUTextureFormat {format} has no framework equivalent."
            ),
        };
    }

    // ---------------------------------------------------------------- SampleCount

    public static GPUSampleCount ConvertSampleCount(SampleCount count)
    {
        if (count == 0)
            count = SampleCount.Count1;

        return count switch
        {
            SampleCount.Count1 => GPUSampleCount.SampleCount1,
            SampleCount.Count2 => GPUSampleCount.SampleCount2,
            SampleCount.Count4 => GPUSampleCount.SampleCount4,
            SampleCount.Count8 => GPUSampleCount.SampleCount8,
            _ => throw new NotSupportedException(
                $"SampleCount {count} is not supported by SDL3 GPU (max is 8)."
            ),
        };
    }

    // ---------------------------------------------------------------- BufferUsage

    public static GPUBufferUsageFlags ConvertBufferUsage(BufferUsage usage)
    {
        GPUBufferUsageFlags result = 0;

        if ((usage & BufferUsage.Vertex) != 0)
            result |= GPUBufferUsageFlags.Vertex;
        if ((usage & BufferUsage.Index) != 0)
            result |= GPUBufferUsageFlags.Index;
        if ((usage & BufferUsage.Indirect) != 0)
            result |= GPUBufferUsageFlags.Indirect;
        if ((usage & BufferUsage.StorageRead) != 0)
            result |= GPUBufferUsageFlags.GraphicsStorageRead;
        if ((usage & BufferUsage.StorageWrite) != 0)
            result |= GPUBufferUsageFlags.ComputeStorageWrite;

        // BufferUsage.Dynamic 无对应标志：SDL3 的更新一律走 transfer buffer + copy pass，
        // 上层不需要也不应该用它来区分显存类型。
        //
        // BufferUsage.Uniform 同样无对应标志：SDL3 靠着色器创建时声明的
        // NumUniformBuffers 分配槽位，VkBuffer 上只需要任意一个非空 usage
        // （由 SDL3ResourceFactory.CreateBuffer 兜底补 STORAGE_BUFFER 位）。
        return result;
    }

    // ---------------------------------------------------------------- TextureUsage

    public static GPUTextureUsageFlags ConvertTextureUsage(TextureUsage usage)
    {
        GPUTextureUsageFlags result = 0;

        if ((usage & TextureUsage.Sampled) != 0)
            result |= GPUTextureUsageFlags.Sampler;
        if ((usage & TextureUsage.ColorTarget) != 0)
            result |= GPUTextureUsageFlags.ColorTarget;
        if ((usage & TextureUsage.DepthStencilTarget) != 0)
            result |= GPUTextureUsageFlags.DepthStencilTarget;
        if ((usage & TextureUsage.StorageRead) != 0)
            result |= GPUTextureUsageFlags.GraphicsStorageRead;
        if ((usage & TextureUsage.StorageWrite) != 0)
            result |= GPUTextureUsageFlags.ComputeStorageWrite;

        return result;
    }

    public static TextureUsage ConvertTextureUsage(GPUTextureUsageFlags usage)
    {
        TextureUsage result = 0;

        if ((usage & GPUTextureUsageFlags.Sampler) != 0)
            result |= TextureUsage.Sampled;
        if ((usage & GPUTextureUsageFlags.ColorTarget) != 0)
            result |= TextureUsage.ColorTarget;
        if ((usage & GPUTextureUsageFlags.DepthStencilTarget) != 0)
            result |= TextureUsage.DepthStencilTarget;
        if ((usage & GPUTextureUsageFlags.GraphicsStorageRead) != 0)
            result |= TextureUsage.StorageRead;
        if ((usage & GPUTextureUsageFlags.ComputeStorageWrite) != 0)
            result |= TextureUsage.StorageWrite;

        return result;
    }

    // ---------------------------------------------------------------- VertexFormat

    /// <summary>
    /// 顶点格式 —— 纯换名映射。
    /// <para>
    /// 本框架的 <see cref="VertexFormat"/> 就是按 SDL3 的命名体系定义的
    /// （Byte = 8 位有符号，UByte = 8 位无符号），所以这里只有
    /// Float1/Int1/UInt1 三个名字不同，其余逐字对应。
    /// </para>
    /// </summary>
    public static GPUVertexElementFormat ConvertVertexFormat(VertexFormat format)
    {
        return format switch
        {
            VertexFormat.Float1 => GPUVertexElementFormat.Float,
            VertexFormat.Float2 => GPUVertexElementFormat.Float2,
            VertexFormat.Float3 => GPUVertexElementFormat.Float3,
            VertexFormat.Float4 => GPUVertexElementFormat.Float4,

            VertexFormat.Int1 => GPUVertexElementFormat.Int,
            VertexFormat.Int2 => GPUVertexElementFormat.Int2,
            VertexFormat.Int3 => GPUVertexElementFormat.Int3,
            VertexFormat.Int4 => GPUVertexElementFormat.Int4,

            VertexFormat.UInt1 => GPUVertexElementFormat.Uint,
            VertexFormat.UInt2 => GPUVertexElementFormat.Uint2,
            VertexFormat.UInt3 => GPUVertexElementFormat.Uint3,
            VertexFormat.UInt4 => GPUVertexElementFormat.Uint4,

            VertexFormat.Short2 => GPUVertexElementFormat.Short2,
            VertexFormat.Short4 => GPUVertexElementFormat.Short4,
            VertexFormat.Short2Norm => GPUVertexElementFormat.Short2Norm,
            VertexFormat.Short4Norm => GPUVertexElementFormat.Short4Norm,

            VertexFormat.UShort2 => GPUVertexElementFormat.Ushort2,
            VertexFormat.UShort4 => GPUVertexElementFormat.Ushort4,
            VertexFormat.UShort2Norm => GPUVertexElementFormat.Ushort2Norm,
            VertexFormat.UShort4Norm => GPUVertexElementFormat.Ushort4Norm,

            VertexFormat.Half2 => GPUVertexElementFormat.Half2,
            VertexFormat.Half4 => GPUVertexElementFormat.Half4,

            VertexFormat.Byte2 => GPUVertexElementFormat.Byte2,
            VertexFormat.Byte4 => GPUVertexElementFormat.Byte4,
            VertexFormat.Byte2Norm => GPUVertexElementFormat.Byte2Norm,
            VertexFormat.Byte4Norm => GPUVertexElementFormat.Byte4Norm,

            VertexFormat.UByte2 => GPUVertexElementFormat.Ubyte2,
            VertexFormat.UByte4 => GPUVertexElementFormat.Ubyte4,
            VertexFormat.UByte2Norm => GPUVertexElementFormat.Ubyte2Norm,
            VertexFormat.UByte4Norm => GPUVertexElementFormat.Ubyte4Norm,

            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    // ---------------------------------------------------------------- 图元 / 光栅

    public static GPUPrimitiveType ConvertPrimitiveTopology(PrimitiveTopology topology)
    {
        return topology switch
        {
            PrimitiveTopology.TriangleList => GPUPrimitiveType.TriangleList,
            PrimitiveTopology.TriangleStrip => GPUPrimitiveType.TriangleStrip,
            PrimitiveTopology.LineList => GPUPrimitiveType.LineList,
            PrimitiveTopology.LineStrip => GPUPrimitiveType.LineStrip,
            PrimitiveTopology.PointList => GPUPrimitiveType.PointList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null),
        };
    }

    public static GPURasterizerState ConvertRasterizerState(RasterizerStateDescription des)
    {
        return new GPURasterizerState
        {
            FillMode =
                des.FillMode == PolygonFillMode.Wireframe ? GPUFillMode.Line : GPUFillMode.Fill,
            CullMode = des.CullMode switch
            {
                FaceCullMode.None => GPUCullMode.None,
                FaceCullMode.Front => GPUCullMode.Front,
                FaceCullMode.Back => GPUCullMode.Back,
                _ => GPUCullMode.None,
            },
            FrontFace =
                des.FrontFace == FrontFace.Clockwise
                    ? GPUFrontFace.Clockwise
                    : GPUFrontFace.CounterClockwise,
            DepthBiasConstantFactor = 0,
            DepthBiasClamp = 0,
            DepthBiasSlopeFactor = 0,
        };
    }

    // ---------------------------------------------------------------- 混合

    public static GPUBlendFactor ConvertBlendFactor(BlendFactor factor, bool alpha)
    {
        return factor switch
        {
            BlendFactor.Zero => GPUBlendFactor.Zero,
            BlendFactor.One => GPUBlendFactor.One,
            BlendFactor.SourceColor => alpha ? GPUBlendFactor.SrcAlpha : GPUBlendFactor.SrcColor,
            BlendFactor.InverseSourceColor => alpha
                ? GPUBlendFactor.OneMinusSrcAlpha
                : GPUBlendFactor.OneMinusSrcColor,
            BlendFactor.SourceAlpha => GPUBlendFactor.SrcAlpha,
            BlendFactor.InverseSourceAlpha => GPUBlendFactor.OneMinusSrcAlpha,
            BlendFactor.DestinationColor => alpha
                ? GPUBlendFactor.DstAlpha
                : GPUBlendFactor.DstColor,
            BlendFactor.InverseDestinationColor => alpha
                ? GPUBlendFactor.OneMinusDstAlpha
                : GPUBlendFactor.OneMinusDstColor,
            BlendFactor.DestinationAlpha => GPUBlendFactor.DstAlpha,
            BlendFactor.InverseDestinationAlpha => GPUBlendFactor.OneMinusDstAlpha,
            BlendFactor.BlendConstant => GPUBlendFactor.ConstantColor,
            BlendFactor.InverseBlendConstant => GPUBlendFactor.OneMinusConstantColor,
            BlendFactor.SourceAlphaSaturate => GPUBlendFactor.SrcAlphaSaturate,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
        };
    }

    public static GPUBlendOp ConvertBlendOperation(BlendOperation op)
    {
        return op switch
        {
            BlendOperation.Add => GPUBlendOp.Add,
            BlendOperation.Subtract => GPUBlendOp.Subtract,
            BlendOperation.ReverseSubtract => GPUBlendOp.ReverseSubtract,
            BlendOperation.Min => GPUBlendOp.Min,
            BlendOperation.Max => GPUBlendOp.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };
    }

    /// <summary>
    /// 单个颜色附件的混合状态 → SDL3 的 GPUColorTargetDescription。
    /// </summary>
    public static GPUColorTargetDescription ConvertColorTargetDescription(
        BlendAttachmentDescription blend,
        PixelFormat format
    )
    {
        return new GPUColorTargetDescription
        {
            Format = ConvertPixelFormat(format),
            BlendState = new GPUColorTargetBlendState
            {
                EnableBlend = blend.BlendEnabled,
                EnableColorWriteMask = blend.ColorWriteMask.HasValue,
                ColorWriteMask = blend.ColorWriteMask.HasValue
                    ? ConvertColorWriteMask(blend.ColorWriteMask.Value)
                    : GPUColorComponentFlags.R
                        | GPUColorComponentFlags.G
                        | GPUColorComponentFlags.B
                        | GPUColorComponentFlags.A,
                SrcColorBlendFactor = ConvertBlendFactor(blend.SourceColorFactor, false),
                DstColorBlendFactor = ConvertBlendFactor(blend.DestinationColorFactor, false),
                ColorBlendOp = ConvertBlendOperation(blend.ColorFunction),
                SrcAlphaBlendFactor = ConvertBlendFactor(blend.SourceAlphaFactor, true),
                DstAlphaBlendFactor = ConvertBlendFactor(blend.DestinationAlphaFactor, true),
                AlphaBlendOp = ConvertBlendOperation(blend.AlphaFunction),
            },
        };
    }

    public static GPUColorComponentFlags ConvertColorWriteMask(ColorWriteMask mask)
    {
        GPUColorComponentFlags result = 0;
        if ((mask & ColorWriteMask.Red) != 0)
            result |= GPUColorComponentFlags.R;
        if ((mask & ColorWriteMask.Green) != 0)
            result |= GPUColorComponentFlags.G;
        if ((mask & ColorWriteMask.Blue) != 0)
            result |= GPUColorComponentFlags.B;
        if ((mask & ColorWriteMask.Alpha) != 0)
            result |= GPUColorComponentFlags.A;
        return result;
    }

    // ---------------------------------------------------------------- Sampler

    public static GPUFilter ConvertFilter(SampleFilter f) =>
        f == SampleFilter.Linear ? GPUFilter.Linear : GPUFilter.Nearest;

    public static GPUSamplerMipmapMode ConvertMipmapMode(MipmapMode m) =>
        m == MipmapMode.Linear ? GPUSamplerMipmapMode.Linear : GPUSamplerMipmapMode.Nearest;

    public static GPUSamplerAddressMode ConvertAddressMode(SamplerAddressMode m)
    {
        return m switch
        {
            SamplerAddressMode.Wrap => GPUSamplerAddressMode.Repeat,
            SamplerAddressMode.Mirror => GPUSamplerAddressMode.MirroredRepeat,
            SamplerAddressMode.Clamp => GPUSamplerAddressMode.ClampToEdge,
            // SDL3 的 Border 需要额外的 border color 支持，这里退化为 Clamp
            SamplerAddressMode.Border => GPUSamplerAddressMode.ClampToEdge,
            _ => throw new ArgumentOutOfRangeException(nameof(m), m, null),
        };
    }

    public static GPUSamplerCreateInfo ConvertSamplerDescription(SamplerCreateInfo ci)
    {
        return new GPUSamplerCreateInfo
        {
            MinFilter = ConvertFilter(ci.MinFilter),
            MagFilter = ConvertFilter(ci.MagFilter),
            MipmapMode = ConvertMipmapMode(ci.MipmapMode),
            AddressModeU = ConvertAddressMode(ci.AddressU),
            AddressModeV = ConvertAddressMode(ci.AddressV),
            AddressModeW = ConvertAddressMode(ci.AddressW),
            MipLodBias = ci.LodBias,
            MaxAnisotropy = ci.AnisotropyEnabled ? Math.Max(1u, ci.MaxAnisotropy) : 1f,
            CompareOp = ci.CompareFunction.HasValue
                ? ConvertCompareOp(ci.CompareFunction.Value)
                : GPUCompareOp.Invalid,
            MinLod = ci.MinLod,
            // MaxLod 的 0 是有效值（仅采样 mip0），不是「未设置」。
            // 此处曾把 0 兜底成 1，导致只上传了 mip0 的纹理去和未初始化的
            // mip1 做线性混合 —— 表现为贴图变淡、随旋转闪、甚至整块消失。
            // Veldrid 侧是直接透传的，这里必须保持一致。
            MaxLod = ci.MaxLod,
        };
    }

    public static GPUCompareOp ConvertCompareOp(CompareFunction f)
    {
        return f switch
        {
            CompareFunction.Never => GPUCompareOp.Never,
            CompareFunction.Less => GPUCompareOp.Less,
            CompareFunction.Equal => GPUCompareOp.Equal,
            CompareFunction.LessOrEqual => GPUCompareOp.LessOrEqual,
            CompareFunction.Greater => GPUCompareOp.Greater,
            CompareFunction.NotEqual => GPUCompareOp.NotEqual,
            CompareFunction.GreaterOrEqual => GPUCompareOp.GreaterOrEqual,
            CompareFunction.Always => GPUCompareOp.Always,
            _ => throw new ArgumentOutOfRangeException(nameof(f), f, null),
        };
    }

    // ---------------------------------------------------------------- Texture

    public static GPUTextureCreateInfo ConvertTextureDescription(TextureCreateInfo ci)
    {
        if (ci.Width == 0)
            throw new ArgumentException(
                "TextureCreateInfo.Width is 0 (struct default); set it explicitly.",
                nameof(ci)
            );

        var type = ci.Type == 0 ? TextureType.Texture2D : ci.Type;

        return new GPUTextureCreateInfo
        {
            Type = ConvertTextureType(type),
            Format = ConvertPixelFormat(ci.Format),
            Usage = ConvertTextureUsage(ci.Usage),
            Width = ci.Width,
            Height = ci.Height == 0 ? 1u : ci.Height,
            LayerCountOrDepth = type switch
            {
                TextureType.Texture3D => ci.Depth == 0 ? 1u : ci.Depth,
                _ => ci.ArrayLayers == 0 ? 1u : ci.ArrayLayers,
            },
            NumLevels = ci.MipLevels == 0 ? 1u : ci.MipLevels,
            SampleCount = ConvertSampleCount(ci.SampleCount),
        };
    }

    public static GPUTextureType ConvertTextureType(TextureType t)
    {
        return t switch
        {
            // SDL3 只有 2D / 2DArray / 3D / Cube / CubeArray 五种
            TextureType.Texture1D or TextureType.Texture2D => GPUTextureType.TextureType2D,
            TextureType.Texture1DArray or TextureType.Texture2DArray =>
                GPUTextureType.TextureType2DArray,
            TextureType.Texture3D => GPUTextureType.TextureType3D,
            TextureType.TextureCube => GPUTextureType.TextureTypeCube,
            TextureType.TextureCubeArray => GPUTextureType.TextureTypeCubeArray,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null),
        };
    }

    // ---------------------------------------------------------------- Shader

    public static GPUShaderStage ConvertShaderStage(ShaderStage stage) =>
        stage == ShaderStage.Vertex ? GPUShaderStage.Vertex : GPUShaderStage.Fragment;

    // ---------------------------------------------------------------- 颜色

    public static FColor ConvertColor(RgbaFloat c) => new(c.R, c.G, c.B, c.A);
}

// ShaderFormatSniffer 已移到 Line.Framework.Default.Graphics（两个后端共用），
// 见 Graphics/ShaderFormatSniffer.cs。本命名空间内的调用点无需限定名即可解析到它。
