using Line.Framework.IO;

namespace Line.Framework.Graphics;

/// <summary>
/// 渲染器基类：把合成器输出的顶点流画到窗口上。
/// <para>
/// <b>注意这是旧设计的一部分</b>：它直接依赖 <c>WindowType</c>，
/// 与 <see cref="IGraphicsDevice"/> 那套抽象层并行存在，尚未迁移。
/// 新代码应基于 IWindow / ISurface 链路，不要往这里继续加依赖。
/// </para>
/// </summary>
public abstract class RendererType : IDisposable
{
    /// <summary>
    /// 纹理资源布局（抽象层视角）。
    /// 由子类在初始化时创建，供纹理资源集复用。
    /// </summary>
    public virtual IResourceLayout TextureLayout { get; }

    /// <summary>释放渲染器持有的 GPU 资源（管线、缓冲、纹理等）。</summary>
    public abstract void Dispose();

    /// <summary>
    /// 绘制一帧的顶点数据（按提交顺序绘制）。
    /// </summary>
    /// <param name="vertices">
    /// 合成器输出的顶点流，位置为屏幕像素坐标；
    /// 实现负责换算到 NDC，并按纹理 / 资源集分批提交。
    /// </param>
    /// <param name="frameBuffer">
    /// 本次绘制的目标。传交换链的帧缓冲即为呈现到窗口。
    /// </param>
    public abstract void Render(IEnumerable<Vertex> vertices, IFrameBuffer frameBuffer);

    /// <summary>宿主窗口（旧设计的耦合点）。</summary>
    protected virtual WindowType Host { get; }

    /// <summary>把渲染器绑定到某个窗口。</summary>
    protected RendererType(WindowType window)
    {
        Host = window;
    }
}
