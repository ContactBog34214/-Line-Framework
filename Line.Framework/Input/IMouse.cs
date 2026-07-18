using System.Numerics;

namespace Line.Framework.Input;

public interface IMouse:ICursor
{
    Vector2 WheelDelta { get; }
    bool IsMouseButtonDown(MouseButton Button);
}

public enum MouseButton
{
    Left = 1, // SDL_BUTTON_LEFT
    Middle = 2, // SDL_BUTTON_MIDDLE
    Right = 3, // SDL_BUTTON_RIGHT
    X1 = 4, // SDL_BUTTON_X1 (侧边按钮1)
    X2 = 5, // SDL_BUTTON_X2 (侧边按钮2)
}
