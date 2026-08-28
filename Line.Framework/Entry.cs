using System.Collections.Concurrent;
using System.Diagnostics;

namespace Line.Framework;

public static class Entry
{
    private static ConcurrentDictionary<Func<CancellationToken, Task>, double> Funcs = new();
    /// <summary>
    /// 基准频率(单位=秒)
    /// </summary>
    public static double BaseFrequency { get; set; } = 10000;
    private static Stopwatch stopwatch = new();
    /// <summary>
    /// 新增主线程托管任务
    /// </summary>
    /// <param name="任务"></param>
    /// <param name="频率"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void AddFunc(Func<CancellationToken, Task> func, double Frequency)
    {
        if (Frequency <= 0 && Frequency != -1) throw new InvalidOperationException($"Frequency cannot be {Frequency}");
        if (!Funcs.TryAdd(func, Frequency)) throw new InvalidOperationException("Action already exists");
    }
    /// <summary>
    /// 更新主线程托管任务频率
    /// </summary>
    /// <param name="任务"></param>
    /// <param name="频率"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void UpdateFunc(Func<CancellationToken, Task> func, double Frequency)
    {
        if (Frequency <= 0 && Frequency != -1) throw new InvalidOperationException($"Frequency cannot be {Frequency}");
        if (!Funcs.TryGetValue(func, out var val)) throw new InvalidOperationException("Action does not exist");
        Funcs.TryUpdate(func, Frequency, val);
    }
    /// <summary>
    /// 获取主线程托管任务频率
    /// </summary>
    /// <param name="任务"></param>
    /// <returns>频率</returns>
    public static double GetFuncFrequency(Func<CancellationToken, Task> func)
    {
        if (Funcs.TryGetValue(func, out var val)) return val;
        return 0;
    }
    /// <summary>
    /// 移除托管任务
    /// </summary>
    /// <param name="任务"></param>
    public static void RemoveFunc(Func<CancellationToken, Task> func)
    {
        Funcs.TryRemove(func, out _);
    }
    private static bool MainThreadRunning = false;
    private static CancellationTokenSource token = new();
    private static Task MainTask = null;
    private static Task LoopTask = null;
    /// <summary>
    /// 附带托管运行主程序
    /// </summary>
    /// <param name="主程序代码"></param>
    /// <param name="参数"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task Run(Func<CancellationToken, string[], Task> main, string[] args)
    {
        if (MainThreadRunning) throw new InvalidOperationException($"Main is already running");
        MainThreadRunning = true;
        token = new();
        try
        {
            MainTask = main(token.Token, args);
            LoopTask = MainLoop();
            await MainTask;
        }
        finally
        {
            MainThreadRunning = false;
        }
        if (LoopTask != null) await LoopTask;
    }
    /// <summary>
    /// 停止主程序运行
    /// </summary>
    /// <returns></returns>
    public static async Task Cancel()
    {
        if (!MainThreadRunning) return;
        if (token != null) await token.CancelAsync();
    }
    /// <summary>
    /// 杀死主程序
    /// </summary>
    public static void ForceStop()
    {
        if (token != null) token.Cancel();
        if (!MainThreadRunning) return;
        MainTask?.Dispose();
        LoopTask?.Dispose();
        MainThreadRunning = false;
    }
    /// <summary>
    /// 是否运行中
    /// </summary>
    public static bool Running => MainThreadRunning;
    private static async Task MainLoop()
    {
        stopwatch.Restart();
        ConcurrentDictionary<Func<CancellationToken, Task>, double> lastExecuteTime = new();
        double LastSleep = 0;
        while (Running && !token.Token.IsCancellationRequested)
        {
            long tick = stopwatch.ElapsedTicks;
            double milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;

            IEnumerable<Task> Executions = Funcs
            .Where(f =>
            {
                double r = 0;
                if (f.Value > 0) r = 1000d / f.Value;
                if (!lastExecuteTime.TryGetValue(f.Key, out var last))
                {
                    lastExecuteTime.TryAdd(f.Key, 0);
                    last = 0;
                }
                if (milliseconds - last < r) return false;
                lastExecuteTime.TryUpdate(f.Key, milliseconds, last);
                return true;
            })
            .Select(f => f.Key.Invoke(token.Token));

            try
            {
                await Task.WhenAll(Executions.Where(f => f != null));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            //休眠
            double wait = 0;
            if (BaseFrequency > 0) wait = 1000d / BaseFrequency;
            tick = stopwatch.ElapsedTicks;
            milliseconds = (double)tick / Stopwatch.Frequency * 1000.0;
            double t = wait - Math.Min(wait, milliseconds - LastSleep);
            LastSleep = milliseconds + t;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(t), token.Token);
            }
            catch (TaskCanceledException) {/*_*/return; }
        }
    }
}