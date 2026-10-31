using Line.Framework.Graphics;
using Line.Framework.Types;

namespace Line.Framework.Default.Graphics.Veldrid;

public static partial class VeldridConverter
{
    public static global::Veldrid.PixelFormat ConvertPixelFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8UNorm => global::Veldrid.PixelFormat.R8_UNorm,

            PixelFormat.R8G8UNorm => global::Veldrid.PixelFormat.R8_G8_UNorm,

            PixelFormat.R8G8B8A8UNorm => global::Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,

            PixelFormat.B8G8R8A8UNorm => global::Veldrid.PixelFormat.B8_G8_R8_A8_UNorm,

            PixelFormat.R16UNorm => global::Veldrid.PixelFormat.R16_UNorm,

            PixelFormat.R16G16UNorm => global::Veldrid.PixelFormat.R16_G16_UNorm,

            PixelFormat.R16G16B16A16UNorm => global::Veldrid.PixelFormat.R16_G16_B16_A16_UNorm,

            PixelFormat.R8SNorm => global::Veldrid.PixelFormat.R8_SNorm,

            PixelFormat.R8G8SNorm => global::Veldrid.PixelFormat.R8_G8_SNorm,

            PixelFormat.R8G8B8A8SNorm => global::Veldrid.PixelFormat.R8_G8_B8_A8_SNorm,

            PixelFormat.R16SNorm => global::Veldrid.PixelFormat.R16_SNorm,

            PixelFormat.R16G16SNorm => global::Veldrid.PixelFormat.R16_G16_SNorm,

            PixelFormat.R16G16B16A16SNorm => global::Veldrid.PixelFormat.R16_G16_B16_A16_SNorm,

            PixelFormat.R16Float => global::Veldrid.PixelFormat.R16_Float,

            PixelFormat.R16G16Float => global::Veldrid.PixelFormat.R16_G16_Float,

            PixelFormat.R16G16B16A16Float => global::Veldrid.PixelFormat.R16_G16_B16_A16_Float,

            PixelFormat.R32Float => global::Veldrid.PixelFormat.R32_Float,

            PixelFormat.R32G32Float => global::Veldrid.PixelFormat.R32_G32_Float,

            PixelFormat.R32G32B32A32Float => global::Veldrid.PixelFormat.R32_G32_B32_A32_Float,

            PixelFormat.R8UInt => global::Veldrid.PixelFormat.R8_UInt,

            PixelFormat.R8G8UInt => global::Veldrid.PixelFormat.R8_G8_UInt,

            PixelFormat.R8G8B8A8UInt => global::Veldrid.PixelFormat.R8_G8_B8_A8_UInt,

            PixelFormat.R8SInt => global::Veldrid.PixelFormat.R8_SInt,

            PixelFormat.R8G8SInt => global::Veldrid.PixelFormat.R8_G8_SInt,

            PixelFormat.R8G8B8A8SInt => global::Veldrid.PixelFormat.R8_G8_B8_A8_SInt,

            _ => throw new NotSupportedException($"Unsupported pixel format: {format}"),
        };
    }

    public static PixelFormat ConvertPixelFormat(global::Veldrid.PixelFormat format)
    {
        return format switch
        {
            global::Veldrid.PixelFormat.R8_UNorm => PixelFormat.R8UNorm,

            global::Veldrid.PixelFormat.R8_G8_UNorm => PixelFormat.R8G8UNorm,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_UNorm => PixelFormat.R8G8B8A8UNorm,

            global::Veldrid.PixelFormat.B8_G8_R8_A8_UNorm => PixelFormat.B8G8R8A8UNorm,

            global::Veldrid.PixelFormat.R16_UNorm => PixelFormat.R16UNorm,

            global::Veldrid.PixelFormat.R16_G16_UNorm => PixelFormat.R16G16UNorm,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_UNorm => PixelFormat.R16G16B16A16UNorm,

            global::Veldrid.PixelFormat.R8_SNorm => PixelFormat.R8SNorm,

            global::Veldrid.PixelFormat.R8_G8_SNorm => PixelFormat.R8G8SNorm,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_SNorm => PixelFormat.R8G8B8A8SNorm,

            global::Veldrid.PixelFormat.R16_SNorm => PixelFormat.R16SNorm,

            global::Veldrid.PixelFormat.R16_G16_SNorm => PixelFormat.R16G16SNorm,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_SNorm => PixelFormat.R16G16B16A16SNorm,

            global::Veldrid.PixelFormat.R16_Float => PixelFormat.R16Float,

            global::Veldrid.PixelFormat.R16_G16_Float => PixelFormat.R16G16Float,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_Float => PixelFormat.R16G16B16A16Float,

            global::Veldrid.PixelFormat.R32_Float => PixelFormat.R32Float,

            global::Veldrid.PixelFormat.R32_G32_Float => PixelFormat.R32G32Float,

            global::Veldrid.PixelFormat.R32_G32_B32_A32_Float => PixelFormat.R32G32B32A32Float,

            global::Veldrid.PixelFormat.R8_UInt => PixelFormat.R8UInt,

            global::Veldrid.PixelFormat.R8_G8_UInt => PixelFormat.R8G8UInt,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_UInt => PixelFormat.R8G8B8A8UInt,

            global::Veldrid.PixelFormat.R8_SInt => PixelFormat.R8SInt,

            global::Veldrid.PixelFormat.R8_G8_SInt => PixelFormat.R8G8SInt,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_SInt => PixelFormat.R8G8B8A8SInt,

            _ => throw new NotSupportedException($"Unsupported Veldrid pixel format: {format}"),
        };
    }

    public static global::Veldrid.BlendFactor ConvertBlendFactor(BlendFactor factor)
    {
        return factor switch
        {
            BlendFactor.Zero => global::Veldrid.BlendFactor.Zero,

            BlendFactor.One => global::Veldrid.BlendFactor.One,

            BlendFactor.SourceColor => global::Veldrid.BlendFactor.SourceColor,

            BlendFactor.InverseSourceColor => global::Veldrid.BlendFactor.InverseSourceColor,

            BlendFactor.SourceAlpha => global::Veldrid.BlendFactor.SourceAlpha,

            BlendFactor.InverseSourceAlpha => global::Veldrid.BlendFactor.InverseSourceAlpha,

            BlendFactor.DestinationColor => global::Veldrid.BlendFactor.DestinationColor,

            BlendFactor.InverseDestinationColor => global::Veldrid
                .BlendFactor
                .InverseDestinationColor,

            BlendFactor.DestinationAlpha => global::Veldrid.BlendFactor.DestinationAlpha,

            BlendFactor.InverseDestinationAlpha => global::Veldrid
                .BlendFactor
                .InverseDestinationAlpha,

            BlendFactor.BlendConstant => global::Veldrid.BlendFactor.BlendFactor,

            BlendFactor.InverseBlendConstant => global::Veldrid.BlendFactor.InverseBlendFactor,

            BlendFactor.SourceAlphaSaturate => throw new NotSupportedException(
                "Veldrid backend does not support SourceAlphaSaturate."
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null),
        };
    }

    public static global::Veldrid.ColorWriteMask ConvertColorWriteMask(ColorWriteMask mask)
    {
        global::Veldrid.ColorWriteMask result = global::Veldrid.ColorWriteMask.None;

        if ((mask & ColorWriteMask.Red) != 0)
            result |= global::Veldrid.ColorWriteMask.Red;

        if ((mask & ColorWriteMask.Green) != 0)
            result |= global::Veldrid.ColorWriteMask.Green;

        if ((mask & ColorWriteMask.Blue) != 0)
            result |= global::Veldrid.ColorWriteMask.Blue;

        if ((mask & ColorWriteMask.Alpha) != 0)
            result |= global::Veldrid.ColorWriteMask.Alpha;

        return result;
    }

    public static global::Veldrid.BlendFunction ConvertBlendFunction(BlendOperation operation)
    {
        return operation switch
        {
            BlendOperation.Add => global::Veldrid.BlendFunction.Add,
            BlendOperation.Max => global::Veldrid.BlendFunction.Maximum,
            BlendOperation.Min => global::Veldrid.BlendFunction.Minimum,
            BlendOperation.ReverseSubtract => global::Veldrid.BlendFunction.ReverseSubtract,
            BlendOperation.Subtract => global::Veldrid.BlendFunction.Subtract,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }

    public static BlendOperation ConvertBlendFunction(global::Veldrid.BlendFunction func)
    {
        return func switch
        {
            global::Veldrid.BlendFunction.Add => BlendOperation.Add,
            global::Veldrid.BlendFunction.Maximum => BlendOperation.Max,
            global::Veldrid.BlendFunction.Minimum => BlendOperation.Min,
            global::Veldrid.BlendFunction.ReverseSubtract => BlendOperation.ReverseSubtract,
            global::Veldrid.BlendFunction.Subtract => BlendOperation.Subtract,
            _ => throw new ArgumentOutOfRangeException(nameof(func), func, null),
        };
    }

    public static global::Veldrid.RgbaFloat ConvertRgbaFloat(RgbaFloat color) =>
        new(color.R, color.G, color.B, color.A);

    public static RgbaFloat ConvertRgbaFloat(global::Veldrid.RgbaFloat color) =>
        new(color.R, color.G, color.B, color.A);

    public static global::Veldrid.BlendAttachmentDescription ConvertBlendAttachmentDescription(
        BlendAttachmentDescription des
    )
    {
        return new()
        {
            BlendEnabled = des.BlendEnabled,
            AlphaFunction = ConvertBlendFunction(des.AlphaFunction),
            ColorFunction = ConvertBlendFunction(des.ColorFunction),
            ColorWriteMask = des.ColorWriteMask.HasValue
                ? ConvertColorWriteMask(des.ColorWriteMask.Value)
                : null,
            SourceAlphaFactor = ConvertBlendFactor(des.SourceAlphaFactor),
            SourceColorFactor = ConvertBlendFactor(des.SourceColorFactor),
            DestinationAlphaFactor = ConvertBlendFactor(des.DestinationAlphaFactor),
            DestinationColorFactor = ConvertBlendFactor(des.DestinationColorFactor),
        };
    }

    public static global::Veldrid.BlendStateDescription ConvertBlendStateDescription(
        BlendStateDescription des
    )
    {
        return new()
        {
            BlendFactor = ConvertRgbaFloat(des.BlendFactor),
            AttachmentStates = [.. des.AttachmentStates.Select(ConvertBlendAttachmentDescription)],
            AlphaToCoverageEnabled = des.AlphaToCoverageEnabled,
        };
    }

    public static FaceCullMode ConvertFaceCullMode(global::Veldrid.FaceCullMode m)
    {
        return m switch
        {
            global::Veldrid.FaceCullMode.None => FaceCullMode.None,
            global::Veldrid.FaceCullMode.Front => FaceCullMode.Front,
            global::Veldrid.FaceCullMode.Back => FaceCullMode.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(m), m, null),
        };
    }

    public static global::Veldrid.FaceCullMode ConvertFaceCullMode(FaceCullMode m)
    {
        return m switch
        {
            FaceCullMode.None => global::Veldrid.FaceCullMode.None,
            FaceCullMode.Front => global::Veldrid.FaceCullMode.Front,
            FaceCullMode.Back => global::Veldrid.FaceCullMode.Back,
            _ => throw new ArgumentOutOfRangeException(nameof(m), m, null),
        };
    }

    public static global::Veldrid.PolygonFillMode ConvertPolygonFillMode(PolygonFillMode mode)
    {
        return mode switch
        {
            PolygonFillMode.Solid => global::Veldrid.PolygonFillMode.Solid,
            PolygonFillMode.Wireframe => global::Veldrid.PolygonFillMode.Wireframe,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    public static PolygonFillMode ConvertPolygonFillMode(global::Veldrid.PolygonFillMode mode)
    {
        return mode switch
        {
            global::Veldrid.PolygonFillMode.Solid => PolygonFillMode.Solid,
            global::Veldrid.PolygonFillMode.Wireframe => PolygonFillMode.Wireframe,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    public static global::Veldrid.FrontFace ConvertFrontFace(FrontFace face)
    {
        return face switch
        {
            FrontFace.Clockwise => global::Veldrid.FrontFace.Clockwise,
            FrontFace.CounterClockwise => global::Veldrid.FrontFace.CounterClockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
        };
    }

    public static FrontFace ConvertFrontFace(global::Veldrid.FrontFace face)
    {
        return face switch
        {
            global::Veldrid.FrontFace.Clockwise => FrontFace.Clockwise,
            global::Veldrid.FrontFace.CounterClockwise => FrontFace.CounterClockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
        };
    }

    public static global::Veldrid.VertexLayoutDescription ConvertVertexLayoutDescription(
        VertexLayoutDescription des
    )
    {
        return new()
        {
            Stride = des.Stride,
            InstanceStepRate = des.InputRate == VertexInputRate.Instance ? 1u : 0u,
            Elements = [.. des.Attributes.Select(ConvertVertexElementDescription)],
        };
    }

    public static VertexLayoutDescription ConvertVertexLayoutDescription(
        global::Veldrid.VertexLayoutDescription des
    )
    {
        return new()
        {
            Stride = des.Stride,
            InputRate =
                des.InstanceStepRate == 0 ? VertexInputRate.Vertex : VertexInputRate.Instance,
            Attributes = [.. des.Elements.Select(ConvertVertexElementDescription)],
        };
    }

    public static global::Veldrid.VertexElementDescription ConvertVertexElementDescription(
        VertexAttributeDescription des
    )
    {
        return new()
        {
            Offset = des.Offset,
            Name = $"OFFSET{des.Offset}",
            Format = ConvertVertexFormat(des.Format),
            Semantic = global::Veldrid.VertexElementSemantic.TextureCoordinate,
        };
    }

    public static VertexAttributeDescription ConvertVertexElementDescription(
        global::Veldrid.VertexElementDescription des
    )
    {
        return new()
        {
            Offset = des.Offset,
            Format = ConvertVertexFormat(des.Format),
            SizeInBytes = global::Veldrid.FormatSizeHelpers.GetSizeInBytes(des.Format),
        };
    }

    public static global::Veldrid.VertexElementFormat ConvertVertexFormat(VertexFormat format)
    {
        return format switch
        {
            // 32-bit Float
            VertexFormat.Float1 => global::Veldrid.VertexElementFormat.Float1,
            VertexFormat.Float2 => global::Veldrid.VertexElementFormat.Float2,
            VertexFormat.Float3 => global::Veldrid.VertexElementFormat.Float3,
            VertexFormat.Float4 => global::Veldrid.VertexElementFormat.Float4,
            // 32-bit Signed Int
            VertexFormat.Int1 => global::Veldrid.VertexElementFormat.Int1,
            VertexFormat.Int2 => global::Veldrid.VertexElementFormat.Int2,
            VertexFormat.Int3 => global::Veldrid.VertexElementFormat.Int3,
            VertexFormat.Int4 => global::Veldrid.VertexElementFormat.Int4,
            // 32-bit Unsigned Int
            VertexFormat.UInt1 => global::Veldrid.VertexElementFormat.UInt1,
            VertexFormat.UInt2 => global::Veldrid.VertexElementFormat.UInt2,
            VertexFormat.UInt3 => global::Veldrid.VertexElementFormat.UInt3,
            VertexFormat.UInt4 => global::Veldrid.VertexElementFormat.UInt4,
            // 16-bit Signed
            VertexFormat.Short2 => global::Veldrid.VertexElementFormat.Short2,
            VertexFormat.Short4 => global::Veldrid.VertexElementFormat.Short4,
            VertexFormat.Short2Norm => global::Veldrid.VertexElementFormat.Short2_Norm,
            VertexFormat.Short4Norm => global::Veldrid.VertexElementFormat.Short4_Norm,
            // 16-bit Unsigned
            VertexFormat.UShort2 => global::Veldrid.VertexElementFormat.UShort2,
            VertexFormat.UShort4 => global::Veldrid.VertexElementFormat.UShort4,
            VertexFormat.UShort2Norm => global::Veldrid.VertexElementFormat.UShort2_Norm,
            VertexFormat.UShort4Norm => global::Veldrid.VertexElementFormat.UShort4_Norm,
            // 16-bit Float
            VertexFormat.Half2 => global::Veldrid.VertexElementFormat.Half2,
            VertexFormat.Half4 => global::Veldrid.VertexElementFormat.Half4,
            // 8-bit Signed —— 注意：映射到 SByte*
            VertexFormat.Byte2 => global::Veldrid.VertexElementFormat.SByte2,
            VertexFormat.Byte4 => global::Veldrid.VertexElementFormat.SByte4,
            VertexFormat.Byte2Norm => global::Veldrid.VertexElementFormat.SByte2_Norm,
            VertexFormat.Byte4Norm => global::Veldrid.VertexElementFormat.SByte4_Norm,
            // 8-bit Unsigned —— 注意：映射到不带 S 的 Byte*
            VertexFormat.UByte2 => global::Veldrid.VertexElementFormat.Byte2,
            VertexFormat.UByte4 => global::Veldrid.VertexElementFormat.Byte4,
            VertexFormat.UByte2Norm => global::Veldrid.VertexElementFormat.Byte2_Norm,
            VertexFormat.UByte4Norm => global::Veldrid.VertexElementFormat.Byte4_Norm,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    public static VertexFormat ConvertVertexFormat(global::Veldrid.VertexElementFormat format)
    {
        return format switch
        {
            // 32-bit Float
            global::Veldrid.VertexElementFormat.Float1 => VertexFormat.Float1,
            global::Veldrid.VertexElementFormat.Float2 => VertexFormat.Float2,
            global::Veldrid.VertexElementFormat.Float3 => VertexFormat.Float3,
            global::Veldrid.VertexElementFormat.Float4 => VertexFormat.Float4,
            // 32-bit Signed Int
            global::Veldrid.VertexElementFormat.Int1 => VertexFormat.Int1,
            global::Veldrid.VertexElementFormat.Int2 => VertexFormat.Int2,
            global::Veldrid.VertexElementFormat.Int3 => VertexFormat.Int3,
            global::Veldrid.VertexElementFormat.Int4 => VertexFormat.Int4,
            // 32-bit Unsigned Int
            global::Veldrid.VertexElementFormat.UInt1 => VertexFormat.UInt1,
            global::Veldrid.VertexElementFormat.UInt2 => VertexFormat.UInt2,
            global::Veldrid.VertexElementFormat.UInt3 => VertexFormat.UInt3,
            global::Veldrid.VertexElementFormat.UInt4 => VertexFormat.UInt4,
            // 16-bit Signed
            global::Veldrid.VertexElementFormat.Short2 => VertexFormat.Short2,
            global::Veldrid.VertexElementFormat.Short4 => VertexFormat.Short4,
            global::Veldrid.VertexElementFormat.Short2_Norm => VertexFormat.Short2Norm,
            global::Veldrid.VertexElementFormat.Short4_Norm => VertexFormat.Short4Norm,
            // 16-bit Unsigned
            global::Veldrid.VertexElementFormat.UShort2 => VertexFormat.UShort2,
            global::Veldrid.VertexElementFormat.UShort4 => VertexFormat.UShort4,
            global::Veldrid.VertexElementFormat.UShort2_Norm => VertexFormat.UShort2Norm,
            global::Veldrid.VertexElementFormat.UShort4_Norm => VertexFormat.UShort4Norm,
            // 16-bit Float
            global::Veldrid.VertexElementFormat.Half2 => VertexFormat.Half2,
            global::Veldrid.VertexElementFormat.Half4 => VertexFormat.Half4,
            global::Veldrid.VertexElementFormat.Half1 => throw new NotSupportedException(null),
            // 8-bit Signed —— Veldrid 的 SByte* 对应本框架的 Byte*
            global::Veldrid.VertexElementFormat.SByte2 => VertexFormat.Byte2,
            global::Veldrid.VertexElementFormat.SByte4 => VertexFormat.Byte4,
            global::Veldrid.VertexElementFormat.SByte2_Norm => VertexFormat.Byte2Norm,
            global::Veldrid.VertexElementFormat.SByte4_Norm => VertexFormat.Byte4Norm,
            // 8-bit Unsigned —— Veldrid 的 Byte* 对应本框架的 UByte*
            global::Veldrid.VertexElementFormat.Byte2 => VertexFormat.UByte2,
            global::Veldrid.VertexElementFormat.Byte4 => VertexFormat.UByte4,
            global::Veldrid.VertexElementFormat.Byte2_Norm => VertexFormat.UByte2Norm,
            global::Veldrid.VertexElementFormat.Byte4_Norm => VertexFormat.UByte4Norm,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }
}
