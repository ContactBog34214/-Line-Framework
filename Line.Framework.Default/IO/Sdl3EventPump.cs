using System.Diagnostics;
using Line.Framework.IO;
using SDL3;

namespace Line.Framework.Default.IO;

/// <summary>
/// SDL3 的事件泵与分发器。
/// <para>
/// <b>为什么需要它</b>：<c>SDL_PumpEvents</c> / <c>SDL_PollEvent</c> 操作的是
/// <b>进程级全局队列</b>，不区分窗口。所以「泵某个窗口的事件」在 SDL3 上
/// 只能实现为「泵全局 → 按 WindowID 分发到各窗口」。
/// </para>
/// <para>
/// <b>为什么是静态的</b>：全局队列只有一份，泵的节流状态也必须是全局的。
/// 每个窗口各持一份节流状态会导致 N 个窗口每帧泵 N 次。
/// </para>
/// </summary>
internal static class Sdl3EventPump
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// 窗口注册表：ID → 弱引用。
    /// <para>
    /// <b>为什么是弱引用</b>：只靠 <see cref="Unregister"/> 清理，任何一条漏掉
    /// <see cref="Unregister"/> 的路径（构造中途抛异常、双重 Dispose 竞态、
    /// 将来忘记调）都会让窗口连同它的队列永久泄漏。弱引用能兜住这类情况 ——
    /// 死条目在下次 <see cref="TryGet"/> 时惰性清掉。
    /// </para>
    /// <para>
    /// <b>前提：调用方必须自己持有强引用</b>（如 <c>WindowType.Window</c>）。
    /// 若某个 <see cref="SDL3Window"/> 只被本字典持有，它可能在窗口仍在使用的
    /// 时候被 GC 回收，表现为「窗口还在，但再也收不到事件」。
    /// </para>
    /// </summary>
    private static readonly Dictionary<uint, WeakReference<SDL3Window>> Windows = new();

    /// <summary>上一次成功泵的 <see cref="Stopwatch.GetTimestamp"/>。</summary>
    private static long lastPumpTicks;

    /// <summary>
    /// 最近一个获得键盘焦点的窗口 ID。
    /// <para>
    /// 用于给「无归属窗口」的全局事件找一个代管者：当前有焦点就给它，
    /// 没有就回退到这里记的最后一个。
    /// </para>
    /// </summary>
    private static uint lastFocusWindowId;

    public static void Register(SDL3Window window)
    {
        lock (Gate)
            Windows[window.Id] = new WeakReference<SDL3Window>(window);
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

    /// <summary>
    /// 泵一次全局事件并分发。
    /// </summary>
    /// <param name="force">
    /// true = 跳过「本帧已泵过」的判断。
    /// <para>
    /// 用于「我需要立刻拿到最新事件」的场景（例如自建主循环在别处泵过，
    /// 但这里想要保证队列是新的）。正常逐帧调用请传 false。
    /// </para>
    /// </param>
    public static void Pump(bool force)
    {
        if (!force && !TryClaim())
            return;

        SDL.PumpEvents();

        while (SDL.PollEvent(out var evt))
            Dispatch(evt);
    }

    /// <summary>
    /// 认领「本次泵」的资格。
    /// <para>
    /// 用「距上次多久」而不是「哪个窗口先调」来判断：全局泵不区分来源，
    /// A 泵和 B 泵效果完全一样，带上来源反而多一份状态。
    /// </para>
    /// <para>
    /// 阈值取 1ms（同一帧内），不能取几十毫秒 —— 那会让第二个窗口的输入
    /// 延迟一整帧以上。
    /// </para>
    /// </summary>
    private static bool TryClaim()
    {
        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref lastPumpTicks);

        if (now - last < Stopwatch.Frequency / 1000)
            return false;

        Interlocked.Exchange(ref lastPumpTicks, now);
        return true;
    }

    private static void Dispatch(SDL.Event evt)
    {
        var type = (SDL.EventType)evt.Type;

        if (IsWindowScoped(type))
        {
            uint id = evt.Window.WindowID;

            // ① 有归属 ID 且窗口还在 → 正常投递。
            if (id != 0 && TryGet(id, out var target))
            {
                if (type == SDL.EventType.WindowFocusGained)
                    NoteFocus(id);

                target.Enqueue(evt);
                return;
            }

            // ② 有归属 ID 但窗口已不存在 → 直接丢弃，且<b>不转交他人</b>。
            //
            //    这类事件在窗口关闭前后必然出现（WindowDestroyed / 残留的
            //    KeyUp 等），对任何其他窗口都没有意义。旧实现让它 fall through
            //    到全局分支，于是每帧刷「dropped global event」日志。
            if (id != 0)
                return;

            // ③ ID 为 0 → 系统没有告诉我们属于谁，按全局事件处理。
        }

        // 无归属窗口的事件（设备热插拔、剪贴板、ID 为 0 的窗口事件）：
        // 交给焦点窗口代管。
        if (TryGetFresh(out var owner))
        {
            owner.EnqueueGlobal(evt);
            return;
        }

        // 一个窗口都没有（启动瞬间 / 全部已关闭）。
        Log.Debug($"[SDL3] dropped global event {type}: no window available to own it.");
    }

    /// <summary>
    /// 找一个可用的代管窗口：当前焦点 → 最后一个有过焦点的 → 任意一个存活的。
    /// </summary>
    private static bool TryGetFresh(out SDL3Window window)
    {
        var focus = SDL.GetKeyboardFocus();
        if (focus != nint.Zero)
        {
            uint id = SDL.GetWindowID(focus);
            if (id != 0)
            {
                NoteFocus(id);
                if (TryGet(id, out window))
                    return true;
            }
        }

        if (lastFocusWindowId != 0 && TryGet(lastFocusWindowId, out window))
            return true;

        // 记录过的焦点窗口也没了（或从未有过焦点）→ 退到任意存活窗口。
        // 同时顺手清理字典里的死条目。
        return TryGetAny(out window);
    }

    /// <summary>
    /// 取注册表里的任意一个存活窗口，并清掉沿途发现的死条目。
    /// </summary>
    private static bool TryGetAny(out SDL3Window window)
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

    /// <summary>
    /// 按 ID 取窗口。目标已被 GC 回收时顺手删掉死条目。
    /// </summary>
    private static bool TryGet(uint id, out SDL3Window window)
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

    /// <summary>
    /// 该类型的事件是否带窗口归属。
    /// <para>
    /// <b>这是「字段安全性」判断，不是策略判断</b>：只有列在这里的类型才允许读
    /// <c>evt.Window.WindowID</c>。SDL 的事件是 C union，不同成员在同一偏移上
    /// 躺着不同的数据 —— 对没有窗口字段的类型（设备事件、剪贴板等）读出来是
    /// 垃圾值，可能恰好命中一个真实窗口 ID 而被误投递。
    /// </para>
    /// <para>
    /// 列表只列<b>本仓库出现过 / 已验证存在</b>的成员名。未列入的窗口事件会走
    /// 「全局」分支交给焦点窗口 —— 单窗口下行为相同，多窗口时需要补全。
    /// 补的时候务必对照 SDL3-CS 的实际成员名（<c>~/.nuget/packages/sdl3-cs/*/lib/*/*.xml</c>）。
    /// </para>
    /// </summary>
    private static bool IsWindowScoped(SDL.EventType type) =>
        type switch
        {
            SDL.EventType.WindowResized
            or SDL.EventType.WindowFocusGained
            or SDL.EventType.WindowFocusLost
            or SDL.EventType.WindowMouseEnter
            or SDL.EventType.WindowMouseLeave
            or SDL.EventType.WindowRestored
            or SDL.EventType.WindowCloseRequested
            or SDL.EventType.WindowDestroyed
            or SDL.EventType.WindowEnterFullscreen
            or SDL.EventType.WindowLeaveFullscreen
            or SDL.EventType.KeyDown
            or SDL.EventType.KeyUp
            or SDL.EventType.TextInput
            or SDL.EventType.MouseMotion
            or SDL.EventType.MouseButtonDown
            or SDL.EventType.MouseButtonUp
            or SDL.EventType.MouseWheel
            or SDL.EventType.FingerDown
            or SDL.EventType.FingerUp
            or SDL.EventType.FingerMotion => true,

            _ => false,
        };
}
