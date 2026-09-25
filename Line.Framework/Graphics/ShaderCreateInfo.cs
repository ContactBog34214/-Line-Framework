namespace Line.Framework.Graphics;

public struct ShaderCreateInfo
{
    public ShaderStage Stage { get; set; }
    public byte[] Bytes { get; set; }
    public string EntryPoint { get; set; }
}