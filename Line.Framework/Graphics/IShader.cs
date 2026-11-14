using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IShader : IDisposable, IName
{
    string EntryPoint { get; }
    ShaderStage Stage { get; }
}
