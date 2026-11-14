using System.Numerics;

namespace Line.Framework.Graphics;

public struct ShaderCreateInfo
{
    public ShaderStage Stage
    {
        get;
        set
        {
            if (BitOperations.PopCount((uint)value) != 1)
                throw new InvalidDataException($"ShaderStage cannot be{value}");
            field = value;
        }
    }
    public byte[] Bytes { get; set; }
    public string EntryPoint { get; set; }
    public IResourceLayout ResourceLayout { get; set; }
}
