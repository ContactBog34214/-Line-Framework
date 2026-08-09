using System.Numerics;

namespace Line.Framework.IO;

public interface IMouse : ICursor
{
    /// <summary>
    /// 滚轮移动增量
    /// </summary>
    Vector2 WheelDelta { get; }

    /// <summary>
    /// 鼠标按钮是否按下
    /// </summary>
    /// <param name="鼠标按钮"></param>
    /// <returns></returns>
    bool IsMouseButtonDown(MouseButton Button);
}

public enum MouseButton
{
    /// <summary>
    /// 左键
    /// </summary>
    Left = 1, // SDL_BUTTON_LEFT

    /// <summary>
    /// 中键
    /// </summary>
    Middle = 2, // SDL_BUTTON_MIDDLE

    /// <summary>
    /// 右键
    /// </summary>
    Right = 3, // SDL_BUTTON_RIGHT
    X1 = 4, // SDL_BUTTON_X1 (侧边按钮1)
    X2 = 5, // SDL_BUTTON_X2 (侧边按钮2)
}
