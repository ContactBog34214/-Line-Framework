/*
 *本人技术不够，所以日志系统是AI写的
 *感谢DeepSeek的神秘代码
 */

using System.Collections.Concurrent;

namespace Line.Framework;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

// 单条日志消息
internal class LogEntry
{
    public LogLevel Level { get; }
    public string Message { get; }
    public DateTime Time { get; }

    public LogEntry(LogLevel level, string message)
    {
        Level = level;
        Message = message;
        Time = DateTime.Now;
    }

    // 格式化输出： [2025-04-08 14:30:21.123][Info] 玩家上线
    public override string ToString()
    {
        return $"[{Time:yyyy-MM-dd HH:mm:ss.fff}][{Level}] {Message}";
    }
}

//Main
public static class Log
{
    private static readonly ConcurrentQueue<LogEntry> s_queue = new();
    private static readonly CancellationTokenSource s_cancelToken = new();
    private static readonly Task s_workerTask;

    // 配置选项
    private static bool s_enableConsole = true;
    private static string s_logFilePath = "logs/game.log";
    private static LogLevel s_minLevel = LogLevel.Debug;
    private static bool s_enableFileLog = true;

    public static void EnableFileLog(bool enable) => s_enableFileLog = enable;

    static Log()
    {
        // 启动后台线程处理日志队列
        s_workerTask = Task.Run(ProcessQueueAsync, s_cancelToken.Token);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
    }

    // 设置最小输出级别（例如只输出 Warning 以上）
    public static void SetMinLevel(LogLevel level) => s_minLevel = level;

    // 设置是否输出到控制台
    public static void EnableConsole(bool enable) => s_enableConsole = enable;

    // 设置日志文件路径（使用前确保目录存在）
    public static void SetLogFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            s_enableFileLog = false;
            return;
        }
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        s_logFilePath = filePath;
        s_enableFileLog = true;
    }

    // ---------- 公开的日志 API ----------
    public static void Debug(string msg) => Enqueue(LogLevel.Debug, msg);

    public static void Info(string msg) => Enqueue(LogLevel.Info, msg);

    public static void Warning(string msg) => Enqueue(LogLevel.Warning, msg);

    public static void Error(string msg) => Enqueue(LogLevel.Error, msg);

    private static void Enqueue(LogLevel level, string msg)
    {
        if (level < s_minLevel)
            return; // 低于设定级别直接丢弃
        s_queue.Enqueue(new LogEntry(level, msg));
    }

    // 异步处理队列
    private static async Task ProcessQueueAsync()
    {
        while (!s_cancelToken.Token.IsCancellationRequested)
        {
            // 等待有消息，避免空转CPU
            while (s_queue.TryDequeue(out var entry))
            {
                WriteToConsole(entry);
                await WriteToFileAsync(entry);
            }
            await Task.Delay(50); // 无消息时短暂休眠
        }

        // 退出前把剩余日志处理完
        while (s_queue.TryDequeue(out var entry))
        {
            WriteToConsole(entry);
            await WriteToFileAsync(entry);
        }
    }

    private static void WriteToConsole(LogEntry entry)
    {
        if (!s_enableConsole)
            return;
        var originalColor = Console.ForegroundColor;
        // 给不同级别上色，方便阅读
        switch (entry.Level)
        {
            case LogLevel.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case LogLevel.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case LogLevel.Debug:
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
            default:
                Console.ForegroundColor = ConsoleColor.White;
                break;
        }
        Console.WriteLine(entry);
        Console.ForegroundColor = originalColor;
    }

    private static async Task WriteToFileAsync(LogEntry entry)
    {
        if (!s_enableFileLog)
            return;
        try
        {
            // 追加写入，每次打开关闭会稍慢，但延迟统一由后台线程处理，影响不大
            // 也可以保持文件流，但为了简单先这样
            using var sw = new StreamWriter(s_logFilePath, true);
            await sw.WriteLineAsync(entry.ToString());
        }
        catch (Exception ex)
        {
            // 这里不要再写日志，避免死循环，直接输出到控制台错误
            Console.WriteLine($"日志文件写入失败: {ex.Message}");
        }
    }

    // 关闭日志系统（程序退出时调用）
    public static void Shutdown()
    {
        s_cancelToken.Cancel();
        try
        {
            s_workerTask.Wait(2000);
        }
        catch { }
        s_cancelToken.Dispose();
    }
}
