namespace Line.Framework.Graphics;

public interface IShader : IDisposable
{
    string Name { get; }
    string EntryPoint { get; }
    ShaderStage Stage { get; }
}