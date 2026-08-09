using Line.Framework.UI;

namespace Line.Framework.Graphics;

/// <summary>
/// 合成器接口
/// </summary>
public interface ICompositor
{
    /// <summary>
    /// 合成UI控件绘制
    /// </summary>
    /// <param name="起始UI控件"></param>
    /// <returns>顶点数组</returns>
    Task<Vertex[]> Composite(UIWidget Root);
}
