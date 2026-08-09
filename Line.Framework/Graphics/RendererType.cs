using Line.Framework.IO;
using Veldrid;

namespace Line.Framework.Graphics;

/// <summary>
/// 渲染器类型
/// </summary>
public abstract class RendererType : IDisposable
{
    /// <summary>
    /// 材质布局
    /// </summary>
    public virtual ResourceLayout TextureLayout { get; }
    public abstract void Dispose();

    /// <summary>
    /// 渲染顶点
    /// </summary>
    /// <param name="由合成器输出的顶点数组"></param>
    public abstract void Render(Vertex[] vertices);
    protected virtual WindowType Host { get; }

    protected RendererType(WindowType window)
    {
        Host = window;
    }
}
