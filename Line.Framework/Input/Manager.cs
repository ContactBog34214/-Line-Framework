using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;

namespace Line.Framework.Input
{
    public class InputManager
    {
        private readonly Sdl2Window _window;
        private readonly Dictionary<Key, bool> _keyStates = new Dictionary<Key, bool>();
        private readonly Dictionary<MouseButton, bool> _mouseStates =
            new Dictionary<MouseButton, bool>();

        // 对外只读累计值（不重置）
        public Vector2 TotalMouseDelta { get; private set; } = Vector2.Zero;
        public float TotalMouseWheelDelta { get; private set; } = 0f;
        Vector2 LastMousePosition { get; set; } = new();

        // 事件
        public event Action<Key> KeyDown;
        public event Action<Key> KeyUp;
        public event Action<MouseButton> MouseDown;
        public event Action<MouseButton> MouseUp;
        public event Action<float> MouseWheel; // 滚动增量（正值向下/右）
        public event Action<float, float> MouseMove; // dx, dy 增量

        public InputManager(Sdl2Window window)
        {
            _window = window;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            _window.KeyDown += OnKeyDown;
            _window.KeyUp += OnKeyUp;
            _window.MouseDown += OnMouseDown;
            _window.MouseUp += OnMouseUp;
            _window.MouseWheel += OnMouseWheel;
            _window.MouseMove += OnMouseMove;
        }

        private void OnKeyDown(KeyEvent evt)
        {
            _keyStates[evt.Key] = true;
            KeyDown?.Invoke(evt.Key);
        }

        private void OnKeyUp(KeyEvent evt)
        {
            _keyStates[evt.Key] = false;
            KeyUp?.Invoke(evt.Key);
        }

        private void OnMouseDown(MouseEvent evt)
        {
            _mouseStates[evt.MouseButton] = true;
            MouseDown?.Invoke(evt.MouseButton);
        }

        private void OnMouseUp(MouseEvent evt)
        {
            _mouseStates[evt.MouseButton] = false;
            MouseUp?.Invoke(evt.MouseButton);
        }

        private void OnMouseWheel(MouseWheelEventArgs evt)
        {
            float delta = evt.WheelDelta;
            TotalMouseWheelDelta += delta;
            MouseWheel?.Invoke(delta);
        }

        private void OnMouseMove(MouseMoveEventArgs evt)
        {
            float dx = evt.MousePosition.X - LastMousePosition.X;
            float dy = evt.MousePosition.Y - LastMousePosition.Y;
            LastMousePosition = evt.MousePosition;
            TotalMouseDelta += new Vector2(dx, dy);
            MouseMove?.Invoke(dx, dy);
        }

        // 状态查询
        public bool IsKeyDown(Key key) => _keyStates.TryGetValue(key, out bool down) && down;

        public bool IsMouseButtonDown(MouseButton button) =>
            _mouseStates.TryGetValue(button, out bool down) && down;
    }
}
