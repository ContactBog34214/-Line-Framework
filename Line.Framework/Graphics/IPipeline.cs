using Line.Framework.Types;

namespace Line.Framework.Graphics;

public interface IPipeline : IDisposable,IName
{
    PipelineType Type { get; }
}