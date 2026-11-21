using System.Diagnostics;
using S2 = SDL2.SDL;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL2 的事件泵与分发器。
/// <para>
/// 结构与 <see cref="Sdl3EventPump"/> 对齐（SDL2 同样是进程级全局队列），
/// 但事件的「窗口归属」判断规则<b>完全不同</b>，所以是独立的一份实现而不是共用。
/// </para>
/// <para>
/// <b>SDL2 与 SDL3 的事件模型差异</b>：
/// <list type="bullet">
/// <item>SDL3 每种窗口事件是独立类型（<c>WINDOW_RESIZED</c> 等）。</item>
/// <item>SDL2 全部归为 <c>SDL_WINDOWEVENT</c>，具体事件在
///       <c>window.event</c> 子字段（<c>SDL_WINDOWEVENT_RESIZED</c> 等）。</item>
/// </list>
/// 因此下面的 <see cref="IsWindowScoped"/> 不能照抄 SDL3 版本。
/// </para>
/// </summary>
internal static class Sdl2EventPump
{
    private static readonly Lock Gate = new();

    /// <summary>窗口注册表：ID → 弱引用。调用方必须自持强引用。</summary>
    private static readonly Dictionary<uint, WeakReference<SDL2Window>> Windows = new();

    private static long lastPumpTicks;

    /// <summary>最近一个获得键盘焦点的窗口 ID（用于给全局事件找代管者）。</summary>
    private static uint lastFocusWindowId;

    public static void Register(SDL2Window window)
    {
        lock (Gate)
            Windows[window.Id] = new WeakReference<SDL2Window>(window);
    }

    public static void Unregister(uint windowId)
    {
        lock (Gate)
            Windows.Remove(windowId);
    }

    public static void NoteFocus(uint windowId)
    {
        if (windowId != 0)
            lastFocusWindowId = windowId;
    }

    public static void Pump(bool force)
    {
        if (!force && !TryClaim())
            return;

        S2.SDL_PumpEvents();

        while (S2.SDL_PollEvent(out var evt) != 0)
            Dispatch(evt);
    }

    /// <summary>认领「本次泵」的资格：1ms 内只泵一次。</summary>
    private static bool TryClaim()
    {
        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref lastPumpTicks);

        if (now - last < Stopwatch.Frequency / 1000)
            return false;

        Interlocked.Exchange(ref lastPumpTicks, now);
        return true;
    }

    private static void Dispatch(S2.SDL_Event evt)
    {
        var type = (S2.SDL_EventType)evt.type;

        if (IsWindowScoped(type))
        {
            uint id = GetWindowId(type, ref evt);

            // ① 有归属且窗口还在 → 正常投递。
            if (id != 0 && TryGet(id, out var target))
            {
                if (
                    type == S2.SDL_EventType.SDL_WINDOWEVENT
                    && (S2.SDL_WindowEventID)evt.window.windowEvent
                        == S2.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED
                )
                    NoteFocus(id);

                target.Enqueue(evt);
                return;
            }

            // ② 有归属但窗口已不存在 → 静默丢弃。
            if (id != 0)
                return;

            // ③ ID 为 0 → 按全局事件处理。
        }

        if (TryGetFresh(out var owner))
        {
            owner.EnqueueGlobal(evt);
            return;
        }

        Log.Debug($"[SDL2] dropped global event {type}: no window available to own it.");
    }

    /// <summary>
    /// 取事件的窗口 ID。
    /// <para>
    /// 只在 <see cref="IsWindowScoped"/> 为真时调用 —— 对没有 windowID 字段的
    /// 事件类型读这个字段会拿到 union 里的垃圾值。
    /// </para>
    /// </summary>
    private static uint GetWindowId(S2.SDL_EventType type, ref S2.SDL_Event evt)
    {
        return type switch
        {
            S2.SDL_EventType.SDL_WINDOWEVENT => evt.window.windowID,
            S2.SDL_EventType.SDL_KEYDOWN or S2.SDL_EventType.SDL_KEYUP => evt.key.windowID,
            S2.SDL_EventType.SDL_TEXTINPUT => evt.text.windowID,
            S2.SDL_EventType.SDL_TEXTEDITING => evt.edit.windowID,
            S2.SDL_EventType.SDL_MOUSEMOTION => evt.motion.windowID,
            S2.SDL_EventType.SDL_MOUSEBUTTONDOWN or S2.SDL_EventType.SDL_MOUSEBUTTONUP =>
                evt.button.windowID,
            S2.SDL_EventType.SDL_MOUSEWHEEL => evt.wheel.windowID,
            S2.SDL_EventType.SDL_FINGERDOWN
            or S2.SDL_EventType.SDL_FINGERUP
            or S2.SDL_EventType.SDL_FINGERMOTION => evt.tfinger.windowID,
            _ => 0,
        };
    }

    /// <summary>
    /// 该类型的事件是否带窗口归属字段。
    /// <para>
    /// ⚠️ <c>SDL_WINDOWEVENT</c> 是一个「总类」，其成员全部带 windowID；
    /// 具体的子事件（resized / focus 等）在 <c>window.windowEvent</c> 里。
    /// </para>
    /// </summary>
    private static bool IsWindowScoped(S2.SDL_EventType type) =>
        type
            is S2.SDL_EventType.SDL_WINDOWEVENT
                or S2.SDL_EventType.SDL_KEYDOWN
                or S2.SDL_EventType.SDL_KEYUP
                or S2.SDL_EventType.SDL_TEXTINPUT
                or S2.SDL_EventType.SDL_TEXTEDITING
                or S2.SDL_EventType.SDL_MOUSEMOTION
                or S2.SDL_EventType.SDL_MOUSEBUTTONDOWN
                or S2.SDL_EventType.SDL_MOUSEBUTTONUP
                or S2.SDL_EventType.SDL_MOUSEWHEEL
                or S2.SDL_EventType.SDL_FINGERDOWN
                or S2.SDL_EventType.SDL_FINGERUP
                or S2.SDL_EventType.SDL_FINGERMOTION;

    private static bool TryGetFresh(out SDL2Window window)
    {
        var focus = S2.SDL_GetKeyboardFocus();
        if (focus != nint.Zero)
        {
            uint id = S2.SDL_GetWindowID(focus);
            if (id != 0)
            {
                NoteFocus(id);
                if (TryGet(id, out window))
                    return true;
            }
        }

        if (lastFocusWindowId != 0 && TryGet(lastFocusWindowId, out window))
            return true;

        return TryGetAny(out window);
    }

    private static bool TryGetAny(out SDL2Window window)
    {
        List<uint> dead = null;

        lock (Gate)
        {
            foreach (var (id, weak) in Windows)
            {
                if (weak.TryGetTarget(out window))
                    return true;

                (dead ??= new()).Add(id);
            }

            if (dead != null)
                foreach (var id in dead)
                    Windows.Remove(id);
        }

        window = null;
        return false;
    }

    private static bool TryGet(uint id, out SDL2Window window)
    {
        if (id == 0)
        {
            window = null;
            return false;
        }

        lock (Gate)
        {
            if (Windows.TryGetValue(id, out var weak))
            {
                if (weak.TryGetTarget(out window))
                    return true;

                Windows.Remove(id);
            }
        }

        window = null;
        return false;
    }
}
