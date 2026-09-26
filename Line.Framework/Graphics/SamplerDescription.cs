namespace Line.Framework.Graphics;

public struct SamplerCreateInfo
{
    public Filter MinFilter { get; set; }
    public Filter MagFilter { get; set; }
    public MipmapMode MipmapMode { get; set; }

    public SamplerAddressMode AddressU { get; set; }
    public SamplerAddressMode AddressV { get; set; }
    public SamplerAddressMode AddressW { get; set; }

    public float LodBias { get; set; }
    public float MinLod { get; set; }
    public float MaxLod { get; set; }

    public bool AnisotropyEnabled { get; set; }
    public uint MaxAnisotropy { get; set; }

    public CompareFunction? CompareFunction { get; set; }
}