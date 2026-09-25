namespace Line.Framework.Graphics;

public interface IPipeline : IDisposable
{
    PipelineType Type { get; }
    string Name { get; }
}