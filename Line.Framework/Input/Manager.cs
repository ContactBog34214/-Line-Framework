using System;
using System.Collections.Concurrent;
using System.Numerics;
using Veldrid;
using static SDL3.SDL;
using Line.Framework.Graphics;

namespace Line.Framework.Input
{
    public class InputManager
    {
        private readonly BaseWindow _window;
        private readonly ConcurrentDictionary<Keycode, bool> _keyStates = new();
        private readonly ConcurrentDictionary<byte, bool> _mouseStates = new();

        private readonly ConcurrentDictionary<MouseButton, Vector2> _touchStates = new();

        // 对外只读累计值（不重置）
        public Vector2 TotalMouseDelta { get; private set; } = Vector2.Zero;
        public Vector2 TotalMouseWheelDelta { get; private set; } = new();
        Vector2 LastMousePosition { get; set; } = new();

        // 事件
        public event Action<Keycode> KeyDown;
        public event Action<Keycode> KeyUp;
        public event Action<MouseButtonEvent> MouseDown;
        public event Action<MouseButtonEvent> MouseUp;
        public event Action<Vector2> MouseWheel; // 滚动增量（正值向下/右）
        public event Action<float, float> MouseMove; // dx, dy 增量

        public string GetClipBoardText() => GetClipboardText();

        public unsafe InputManager(BaseWindow window)
        {
            _window = window;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            _window.EventPool.TryAdd(EventType.KeyDown, OnKeyDown);
            _window.EventPool.TryAdd(EventType.KeyUp, OnKeyUp);
            _window.EventPool.TryAdd(EventType.MouseButtonDown, OnMouseDown);
            _window.EventPool.TryAdd(EventType.MouseButtonUp, OnMouseUp);
            _window.EventPool.TryAdd(EventType.MouseWheel, OnMouseWheel);
            _window.EventPool.TryAdd(EventType.MouseWheel, OnMouseWheel);
        }

        private void OnKeyDown(Event evt)
        {
            _keyStates[evt.Key.Key] = true;
            KeyDown?.Invoke(evt.Key.Key);
        }

        private void OnKeyUp(Event evt)
        {
            _keyStates[evt.Key.Key] = false;
            KeyDown?.Invoke(evt.Key.Key);
        }

        private void OnMouseDown(Event evt)
        {
            _mouseStates[evt.Button.Button] = true;
            MouseDown?.Invoke(evt.Button);
        }

        private void OnMouseUp(Event evt)
        {
            _mouseStates[evt.Button.Button] = false;
            MouseUp?.Invoke(evt.Button);
        }

        private void OnMouseWheel(Event evt)
        {
            Vector2 delta = new(evt.Wheel.X,evt.Wheel.Y);
            TotalMouseWheelDelta += delta;
            MouseWheel?.Invoke(delta);
        }

        private void OnMouseMove(Event evt)
        {
            float dx = evt.Button.X - LastMousePosition.X;
            float dy = evt.Button.Y - LastMousePosition.Y;
            LastMousePosition = new(dx,dy);
            TotalMouseDelta += new Vector2(dx, dy);
            MouseMove?.Invoke(dx, dy);
        }

        // 状态查询
        public bool IsKeyDown(Keycode key) => _keyStates.TryGetValue(key, out bool down) && down;

        public bool IsMouseButtonDown(byte button) =>
            _mouseStates.TryGetValue(button, out bool down) && down;
    }
}
