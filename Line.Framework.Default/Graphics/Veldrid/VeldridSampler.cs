using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics.Veldrid;

public sealed class VeldridSampler : ISampler
{
    internal readonly global::Veldrid.Sampler sampler;
    public string Name => sampler.Name;

    internal VeldridSampler(global::Veldrid.Sampler s)
    {
        sampler = s;
    }

    public void Dispose()
    {
        sampler?.Dispose();
    }
}

public partial class VeldridResourceFactory
{
    public ISampler CreateSampler(SamplerCreateInfo createInfo)
    {
        var sampler = dev.ResourceFactory.CreateSampler(
            VeldridConverter.ConvertSamplerDescription(createInfo)
        );
        try
        {
            return new VeldridSampler(sampler);
        }
        catch
        {
            sampler?.Dispose();
            throw;
        }
    }
}

public partial class VeldridConverter
{
    public static global::Veldrid.SamplerAddressMode ConvertSamplerAddressMode(
        SamplerAddressMode mode
    )
    {
        return mode switch
        {
            SamplerAddressMode.Wrap => global::Veldrid.SamplerAddressMode.Wrap,

            SamplerAddressMode.Mirror => global::Veldrid.SamplerAddressMode.Mirror,

            SamplerAddressMode.Clamp => global::Veldrid.SamplerAddressMode.Clamp,

            SamplerAddressMode.Border => global::Veldrid.SamplerAddressMode.Border,

            _ => throw new NotSupportedException($"Unsupported sampler address mode: {mode}"),
        };
    }

    public static SamplerAddressMode ConvertSamplerAddressMode(
        global::Veldrid.SamplerAddressMode mode
    )
    {
        return mode switch
        {
            global::Veldrid.SamplerAddressMode.Wrap => SamplerAddressMode.Wrap,

            global::Veldrid.SamplerAddressMode.Mirror => SamplerAddressMode.Mirror,

            global::Veldrid.SamplerAddressMode.Clamp => SamplerAddressMode.Clamp,

            global::Veldrid.SamplerAddressMode.Border => SamplerAddressMode.Border,

            _ => throw new NotSupportedException(
                $"Unsupported Veldrid sampler address mode: {mode}"
            ),
        };
    }

    public static global::Veldrid.SamplerFilter ConvertSamplerFilter(
        SampleFilter minFilter,
        SampleFilter magFilter,
        MipmapMode mipmapMode
    )
    {
        return (minFilter, magFilter, mipmapMode) switch
        {
            (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Nearest) => global::Veldrid
                .SamplerFilter
                .MinPoint_MagPoint_MipPoint,

            (SampleFilter.Nearest, SampleFilter.Nearest, MipmapMode.Linear) => global::Veldrid
                .SamplerFilter
                .MinPoint_MagPoint_MipLinear,

            (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Nearest) => global::Veldrid
                .SamplerFilter
                .MinPoint_MagLinear_MipPoint,

            (SampleFilter.Nearest, SampleFilter.Linear, MipmapMode.Linear) => global::Veldrid
                .SamplerFilter
                .MinPoint_MagLinear_MipLinear,

            (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Nearest) => global::Veldrid
                .SamplerFilter
                .MinLinear_MagPoint_MipPoint,

            (SampleFilter.Linear, SampleFilter.Nearest, MipmapMode.Linear) => global::Veldrid
                .SamplerFilter
                .MinLinear_MagPoint_MipLinear,

            (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Nearest) => global::Veldrid
                .SamplerFilter
                .MinLinear_MagLinear_MipPoint,

            (SampleFilter.Linear, SampleFilter.Linear, MipmapMode.Linear) => global::Veldrid
                .SamplerFilter
                .MinLinear_MagLinear_MipLinear,

            _ => throw new NotSupportedException(),
        };
    }

    public static (
        SampleFilter MinFilter,
        SampleFilter MagFilter,
        MipmapMode MipmapMode
    ) ConvertSamplerFilter(global::Veldrid.SamplerFilter filter)
    {
        return filter switch
        {
            global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipPoint => (
                SampleFilter.Nearest,
                SampleFilter.Nearest,
                MipmapMode.Nearest
            ),

            global::Veldrid.SamplerFilter.MinPoint_MagPoint_MipLinear => (
                SampleFilter.Nearest,
                SampleFilter.Nearest,
                MipmapMode.Linear
            ),

            global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipPoint => (
                SampleFilter.Nearest,
                SampleFilter.Linear,
                MipmapMode.Nearest
            ),

            global::Veldrid.SamplerFilter.MinPoint_MagLinear_MipLinear => (
                SampleFilter.Nearest,
                SampleFilter.Linear,
                MipmapMode.Linear
            ),

            global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipPoint => (
                SampleFilter.Linear,
                SampleFilter.Nearest,
                MipmapMode.Nearest
            ),

            global::Veldrid.SamplerFilter.MinLinear_MagPoint_MipLinear => (
                SampleFilter.Linear,
                SampleFilter.Nearest,
                MipmapMode.Linear
            ),

            global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipPoint => (
                SampleFilter.Linear,
                SampleFilter.Linear,
                MipmapMode.Nearest
            ),

            global::Veldrid.SamplerFilter.MinLinear_MagLinear_MipLinear => (
                SampleFilter.Linear,
                SampleFilter.Linear,
                MipmapMode.Linear
            ),

            global::Veldrid.SamplerFilter.Anisotropic => (
                SampleFilter.Linear,
                SampleFilter.Linear,
                MipmapMode.Linear
            ),

            _ => throw new NotSupportedException(),
        };
    }

    public static global::Veldrid.SamplerDescription ConvertSamplerDescription(
        SamplerCreateInfo createInfo
    )
    {
        return new()
        {
            AddressModeU = ConvertSamplerAddressMode(createInfo.AddressU),
            AddressModeV = ConvertSamplerAddressMode(createInfo.AddressV),
            AddressModeW = ConvertSamplerAddressMode(createInfo.AddressW),
            Filter = createInfo.AnisotropyEnabled
                ? global::Veldrid.SamplerFilter.Anisotropic
                : ConvertSamplerFilter(
                    createInfo.MinFilter,
                    createInfo.MagFilter,
                    createInfo.MipmapMode
                ),
            LodBias = createInfo.LodBias,
            MaximumAnisotropy = createInfo.MaxAnisotropy,
            MaximumLod = createInfo.MaxLod,
            MinimumLod = createInfo.MinLod,
        };
    }

    public static SamplerCreateInfo ConvertSamplerDescription(
        global::Veldrid.SamplerDescription createInfo
    )
    {
        var Filters = ConvertSamplerFilter(createInfo.Filter);
        var isAnisotropy = createInfo.Filter == global::Veldrid.SamplerFilter.Anisotropic;
        return new()
        {
            AddressU = ConvertSamplerAddressMode(createInfo.AddressModeU),
            AddressV = ConvertSamplerAddressMode(createInfo.AddressModeV),
            AddressW = ConvertSamplerAddressMode(createInfo.AddressModeW),
            AnisotropyEnabled = isAnisotropy,
        };
    }

    public static global::Veldrid.TextureSampleCount ConvertSampleCount(SampleCount count)
    {
        // SampleCount 枚举从 Count1 = 1 起、没有 0 值，而所有 CreateInfo / Description
        // 都是纯 struct，未赋值的 SampleCount 恰好是 0。
        // 把 0 归一化为 Count1（非多重采样），调用方就不必每次都显式写这一项。
        if (count == 0)
            count = SampleCount.Count1;

        return count switch
        {
            SampleCount.Count1 => global::Veldrid.TextureSampleCount.Count1,

            SampleCount.Count2 => global::Veldrid.TextureSampleCount.Count2,

            SampleCount.Count4 => global::Veldrid.TextureSampleCount.Count4,

            SampleCount.Count8 => global::Veldrid.TextureSampleCount.Count8,

            SampleCount.Count16 => global::Veldrid.TextureSampleCount.Count16,

            SampleCount.Count32 => global::Veldrid.TextureSampleCount.Count32,

            _ => throw new NotSupportedException($"Unsupported sample count: {count}"),
        };
    }

    public static SampleCount ConvertSampleCount(global::Veldrid.TextureSampleCount count)
    {
        return count switch
        {
            global::Veldrid.TextureSampleCount.Count1 => SampleCount.Count1,

            global::Veldrid.TextureSampleCount.Count2 => SampleCount.Count2,

            global::Veldrid.TextureSampleCount.Count4 => SampleCount.Count4,

            global::Veldrid.TextureSampleCount.Count8 => SampleCount.Count8,

            global::Veldrid.TextureSampleCount.Count16 => SampleCount.Count16,

            global::Veldrid.TextureSampleCount.Count32 => SampleCount.Count32,

            _ => throw new NotSupportedException($"Unsupported Veldrid sample count: {count}"),
        };
    }
}
