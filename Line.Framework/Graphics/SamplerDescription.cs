namespace Line.Framework.Graphics;

public struct SamplerCreateInfo
{
    public SampleFilter MinFilter { get; set; }
    public SampleFilter MagFilter { get; set; }
    public MipmapMode MipmapMode { get; set; }

    public SamplerAddressMode AddressU { get; set; }
    public SamplerAddressMode AddressV { get; set; }
    public SamplerAddressMode AddressW { get; set; }

    public int LodBias { get; set; }
    public uint MinLod { get; set; }
    public uint MaxLod { get; set; }

    public bool AnisotropyEnabled { get; set; }
    public uint MaxAnisotropy { get; set; }

    public CompareFunction? CompareFunction { get; set; }
}