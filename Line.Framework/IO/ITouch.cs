namespace Line.Framework.IO;

public interface ITouchDevice
{
    /// <summary>
    ///  触摸点数据
    /// </summary>
    Dictionary<ulong, ICursor> Touches { get; }

    /// <summary>
    /// 获取触摸点
    /// </summary>
    /// <param name="触摸点ID"></param>
    /// <returns>触摸点</returns>
    ICursor GetTouch(ulong Id);
}
