using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

public static class VeldridConverter
{
    public static global::Veldrid.BufferUsage ConvertBufferUsage(BufferUsage usage)
    {
        global::Veldrid.BufferUsage result = 0;

        if ((usage & BufferUsage.Vertex) != 0)
            result |= global::Veldrid.BufferUsage.VertexBuffer;

        if ((usage & BufferUsage.Index) != 0)
            result |= global::Veldrid.BufferUsage.IndexBuffer;

        if ((usage & BufferUsage.Indirect) != 0)
            result |= global::Veldrid.BufferUsage.IndirectBuffer;

        if ((usage & BufferUsage.StorageWrite) != 0)
            result |= global::Veldrid.BufferUsage.StructuredBufferReadWrite;
        else if ((usage & BufferUsage.StorageRead) != 0)
            result |= global::Veldrid.BufferUsage.StructuredBufferReadOnly;

        return result;
    }
    public static BufferUsage ConvertBufferUsage(global::Veldrid.BufferUsage usage)
    {
        BufferUsage result = 0;

        if ((usage & global::Veldrid.BufferUsage.VertexBuffer) != 0)
            result |= BufferUsage.Vertex;

        if ((usage & global::Veldrid.BufferUsage.IndexBuffer) != 0)
            result |= BufferUsage.Index;

        if ((usage & global::Veldrid.BufferUsage.IndirectBuffer) != 0)
            result |= BufferUsage.Indirect;

        if ((usage & global::Veldrid.BufferUsage.StructuredBufferReadWrite) != 0)
        {
            result |= BufferUsage.StorageRead;
            result |= BufferUsage.StorageWrite;
        }
        else if ((usage & global::Veldrid.BufferUsage.StructuredBufferReadOnly) != 0)
        {
            result |= BufferUsage.StorageRead;
        }

        return result;
    }
    public static global::Veldrid.PixelFormat ConvertPixelFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8UNorm =>
                global::Veldrid.PixelFormat.R8_UNorm,

            PixelFormat.R8G8UNorm =>
                global::Veldrid.PixelFormat.R8_G8_UNorm,

            PixelFormat.R8G8B8A8UNorm =>
                global::Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,

            PixelFormat.B8G8R8A8UNorm =>
                global::Veldrid.PixelFormat.B8_G8_R8_A8_UNorm,


            PixelFormat.R16UNorm =>
                global::Veldrid.PixelFormat.R16_UNorm,

            PixelFormat.R16G16UNorm =>
                global::Veldrid.PixelFormat.R16_G16_UNorm,

            PixelFormat.R16G16B16A16UNorm =>
                global::Veldrid.PixelFormat.R16_G16_B16_A16_UNorm,


            PixelFormat.R8SNorm =>
                global::Veldrid.PixelFormat.R8_SNorm,

            PixelFormat.R8G8SNorm =>
                global::Veldrid.PixelFormat.R8_G8_SNorm,

            PixelFormat.R8G8B8A8SNorm =>
                global::Veldrid.PixelFormat.R8_G8_B8_A8_SNorm,


            PixelFormat.R16SNorm =>
                global::Veldrid.PixelFormat.R16_SNorm,

            PixelFormat.R16G16SNorm =>
                global::Veldrid.PixelFormat.R16_G16_SNorm,

            PixelFormat.R16G16B16A16SNorm =>
                global::Veldrid.PixelFormat.R16_G16_B16_A16_SNorm,


            PixelFormat.R16Float =>
                global::Veldrid.PixelFormat.R16_Float,

            PixelFormat.R16G16Float =>
                global::Veldrid.PixelFormat.R16_G16_Float,

            PixelFormat.R16G16B16A16Float =>
                global::Veldrid.PixelFormat.R16_G16_B16_A16_Float,


            PixelFormat.R32Float =>
                global::Veldrid.PixelFormat.R32_Float,

            PixelFormat.R32G32Float =>
                global::Veldrid.PixelFormat.R32_G32_Float,

            PixelFormat.R32G32B32A32Float =>
                global::Veldrid.PixelFormat.R32_G32_B32_A32_Float,


            PixelFormat.R8UInt =>
                global::Veldrid.PixelFormat.R8_UInt,

            PixelFormat.R8G8UInt =>
                global::Veldrid.PixelFormat.R8_G8_UInt,

            PixelFormat.R8G8B8A8UInt =>
                global::Veldrid.PixelFormat.R8_G8_B8_A8_UInt,


            PixelFormat.R8SInt =>
                global::Veldrid.PixelFormat.R8_SInt,

            PixelFormat.R8G8SInt =>
                global::Veldrid.PixelFormat.R8_G8_SInt,

            PixelFormat.R8G8B8A8SInt =>
                global::Veldrid.PixelFormat.R8_G8_B8_A8_SInt,

            _ => throw new NotSupportedException(
                $"Unsupported pixel format: {format}")
        };
    }
    public static PixelFormat ConvertPixelFormat(global::Veldrid.PixelFormat format)
    {
        return format switch
        {
            global::Veldrid.PixelFormat.R8_UNorm =>
                PixelFormat.R8UNorm,

            global::Veldrid.PixelFormat.R8_G8_UNorm =>
                PixelFormat.R8G8UNorm,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_UNorm =>
                PixelFormat.R8G8B8A8UNorm,

            global::Veldrid.PixelFormat.B8_G8_R8_A8_UNorm =>
                PixelFormat.B8G8R8A8UNorm,


            global::Veldrid.PixelFormat.R16_UNorm =>
                PixelFormat.R16UNorm,

            global::Veldrid.PixelFormat.R16_G16_UNorm =>
                PixelFormat.R16G16UNorm,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_UNorm =>
                PixelFormat.R16G16B16A16UNorm,


            global::Veldrid.PixelFormat.R8_SNorm =>
                PixelFormat.R8SNorm,

            global::Veldrid.PixelFormat.R8_G8_SNorm =>
                PixelFormat.R8G8SNorm,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_SNorm =>
                PixelFormat.R8G8B8A8SNorm,


            global::Veldrid.PixelFormat.R16_SNorm =>
                PixelFormat.R16SNorm,

            global::Veldrid.PixelFormat.R16_G16_SNorm =>
                PixelFormat.R16G16SNorm,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_SNorm =>
                PixelFormat.R16G16B16A16SNorm,


            global::Veldrid.PixelFormat.R16_Float =>
                PixelFormat.R16Float,

            global::Veldrid.PixelFormat.R16_G16_Float =>
                PixelFormat.R16G16Float,

            global::Veldrid.PixelFormat.R16_G16_B16_A16_Float =>
                PixelFormat.R16G16B16A16Float,


            global::Veldrid.PixelFormat.R32_Float =>
                PixelFormat.R32Float,

            global::Veldrid.PixelFormat.R32_G32_Float =>
                PixelFormat.R32G32Float,

            global::Veldrid.PixelFormat.R32_G32_B32_A32_Float =>
                PixelFormat.R32G32B32A32Float,


            global::Veldrid.PixelFormat.R8_UInt =>
                PixelFormat.R8UInt,

            global::Veldrid.PixelFormat.R8_G8_UInt =>
                PixelFormat.R8G8UInt,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_UInt =>
                PixelFormat.R8G8B8A8UInt,


            global::Veldrid.PixelFormat.R8_SInt =>
                PixelFormat.R8SInt,

            global::Veldrid.PixelFormat.R8_G8_SInt =>
                PixelFormat.R8G8SInt,

            global::Veldrid.PixelFormat.R8_G8_B8_A8_SInt =>
                PixelFormat.R8G8B8A8SInt,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid pixel format: {format}")
        };
    }
    public static global::Veldrid.TextureSampleCount ConvertSampleCount(SampleCount count)
    {
        return count switch
        {
            SampleCount.Count1 =>
                global::Veldrid.TextureSampleCount.Count1,

            SampleCount.Count2 =>
                global::Veldrid.TextureSampleCount.Count2,

            SampleCount.Count4 =>
                global::Veldrid.TextureSampleCount.Count4,

            SampleCount.Count8 =>
                global::Veldrid.TextureSampleCount.Count8,

            SampleCount.Count16 =>
                global::Veldrid.TextureSampleCount.Count16,

            SampleCount.Count32 =>
                global::Veldrid.TextureSampleCount.Count32,

            _ => throw new NotSupportedException(
                $"Unsupported sample count: {count}")
        };
    }
    public static SampleCount ConvertSampleCount(global::Veldrid.TextureSampleCount count)
    {
        return count switch
        {
            global::Veldrid.TextureSampleCount.Count1 =>
                SampleCount.Count1,

            global::Veldrid.TextureSampleCount.Count2 =>
                SampleCount.Count2,

            global::Veldrid.TextureSampleCount.Count4 =>
                SampleCount.Count4,

            global::Veldrid.TextureSampleCount.Count8 =>
                SampleCount.Count8,

            global::Veldrid.TextureSampleCount.Count16 =>
                SampleCount.Count16,

            global::Veldrid.TextureSampleCount.Count32 =>
                SampleCount.Count32,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid sample count: {count}")
        };
    }
    public static global::Veldrid.TextureType ConvertTextureType(TextureType type)
    {
        return type switch
        {
            TextureType.Texture1D =>
                global::Veldrid.TextureType.Texture1D,

            TextureType.Texture2D =>
                global::Veldrid.TextureType.Texture2D,

            TextureType.Texture3D =>
                global::Veldrid.TextureType.Texture3D,

            _ => throw new NotSupportedException(
                $"Unsupported texture type: {type}")
        };
    }
    public static TextureType ConvertTextureType(global::Veldrid.TextureType type)
    {
        return type switch
        {
            global::Veldrid.TextureType.Texture1D =>
                TextureType.Texture1D,

            global::Veldrid.TextureType.Texture2D =>
                TextureType.Texture2D,

            global::Veldrid.TextureType.Texture3D =>
                TextureType.Texture3D,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid texture type: {type}")
        };
    }
    public static global::Veldrid.TextureUsage ConvertTextureUsage(TextureUsage usage)
    {
        global::Veldrid.TextureUsage result = 0;

        if ((usage & TextureUsage.Sampled) != 0)
            result |= global::Veldrid.TextureUsage.Sampled;

        if ((usage & TextureUsage.ColorTarget) != 0)
            result |= global::Veldrid.TextureUsage.RenderTarget;

        if ((usage & TextureUsage.DepthStencilTarget) != 0)
            result |= global::Veldrid.TextureUsage.DepthStencil;

        if ((usage & TextureUsage.StorageRead) != 0 ||
            (usage & TextureUsage.StorageWrite) != 0)
            result |= global::Veldrid.TextureUsage.Storage;

        return result;
    }
    public static TextureUsage ConvertTextureUsage(global::Veldrid.TextureUsage usage)
    {
        TextureUsage result = 0;

        if ((usage & global::Veldrid.TextureUsage.Sampled) != 0)
            result |= TextureUsage.Sampled;

        if ((usage & global::Veldrid.TextureUsage.RenderTarget) != 0)
            result |= TextureUsage.ColorTarget;

        if ((usage & global::Veldrid.TextureUsage.DepthStencil) != 0)
            result |= TextureUsage.DepthStencilTarget;

        if ((usage & global::Veldrid.TextureUsage.Storage) != 0)
        {
            result |= TextureUsage.StorageRead;
            result |= TextureUsage.StorageWrite;
        }

        return result;
    }
    public static global::Veldrid.ShaderStages ConvertShaderStage(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex =>
                global::Veldrid.ShaderStages.Vertex,

            ShaderStage.Fragment =>
                global::Veldrid.ShaderStages.Fragment,

            _ => throw new NotSupportedException(
                $"Unsupported shader stage: {stage}")
        };
    }
    public static ShaderStage ConvertShaderStage(global::Veldrid.ShaderStages stage)
    {
        return stage switch
        {
            global::Veldrid.ShaderStages.Vertex =>
                ShaderStage.Vertex,

            global::Veldrid.ShaderStages.Fragment =>
                ShaderStage.Fragment,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid shader stage: {stage}")
        };
    }
    public static global::Veldrid.SamplerAddressMode ConvertSamplerAddressMode(SamplerAddressMode mode)
    {
        return mode switch
        {
            SamplerAddressMode.Wrap =>
                global::Veldrid.SamplerAddressMode.Wrap,

            SamplerAddressMode.Mirror =>
                global::Veldrid.SamplerAddressMode.Mirror,

            SamplerAddressMode.Clamp =>
                global::Veldrid.SamplerAddressMode.Clamp,

            SamplerAddressMode.Border =>
                global::Veldrid.SamplerAddressMode.Border,

            _ => throw new NotSupportedException(
                $"Unsupported sampler address mode: {mode}")
        };
    }
    public static SamplerAddressMode ConvertSamplerAddressMode(global::Veldrid.SamplerAddressMode mode)
    {
        return mode switch
        {
            global::Veldrid.SamplerAddressMode.Wrap =>
                SamplerAddressMode.Wrap,

            global::Veldrid.SamplerAddressMode.Mirror =>
                SamplerAddressMode.Mirror,

            global::Veldrid.SamplerAddressMode.Clamp =>
                SamplerAddressMode.Clamp,

            global::Veldrid.SamplerAddressMode.Border =>
                SamplerAddressMode.Border,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid sampler address mode: {mode}")
        };
    }
    public static global::Veldrid.SamplerFilter ConvertSamplerFilter(
        SampleFilter minFilter,
        SampleFilter magFilter,
        MipmapMode mipmapMode)
    {
        return (minFilter, magFilter, mipmapMode) switch
        {
            (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Nearest) =>
                global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipPoint,

            (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Linear) =>
                global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipLinear,


            (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Nearest) =>
                global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipPoint,

            (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Linear) =>
                global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipLinear,


            (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Nearest) =>
                global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipPoint,

            (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Linear) =>
                global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipLinear,


            (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Nearest) =>
                global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipPoint,

            (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Linear) =>
                global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipLinear,

            _ => throw new NotSupportedException()
        };
    }
    public static (
        SampleFilter MinFilter,
        SampleFilter MagFilter,
        MipmapMode MipmapMode)
        ConvertSamplerFilter(global::Veldrid.SamplerFilter filter)
    {
        return filter switch
        {
            global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipPoint =>
                (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Nearest),

            global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipLinear =>
                (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Linear),


            global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipPoint =>
                (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Nearest),

            global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipLinear =>
                (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Linear),


            global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipPoint =>
                (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Nearest),

            global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipLinear =>
                (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Linear),


            global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipPoint =>
                (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Nearest),

            global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipLinear =>
                (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Linear),


            global::Veldrid.SamplerFilter.Anisotropic =>
                (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Linear),

            _ => throw new NotSupportedException()
        };
    }

}