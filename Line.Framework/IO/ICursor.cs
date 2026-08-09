using System.Numerics;

namespace Line.Framework.IO;

public interface ICursor
{
    /// <summary>
    /// 绝对位置
    /// </summary>
    Vector2 Position { get; set; }
}
