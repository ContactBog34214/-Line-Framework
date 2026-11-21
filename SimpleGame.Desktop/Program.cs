using Line.Framework;
using LineFramework.GraphicsTest;

namespace SimpleGame.Desktop;

public static class Desktop
{
    /// <summary>
    /// true  = 跑 LineFramework.GraphicsTest 的图形测试（默认开启 UniformBuffer 示例）
    /// false = 跑 SimpleGame 的完整示例（走 Entry 主线程调度）
    /// <para>
    /// 图形测试直接调用、<b>不进 Entry</b>：它自己有渲染循环，
    /// 全程占用主线程（Windows 上 SDL 的视频子系统本身就要求在
    /// 创建窗口的那个线程上调用），套上 Entry.MainLoop 只会让
    /// 主线程调度器在整个窗口生命周期内饿死，没有意义。
    /// </para>
    /// </summary>
    private static bool UseGraphicsTest = false;

    public static async Task Main(string[] args)
    {
        if (args.Contains("--GraphicsTest"))
            UseGraphicsTest = true;
        if (UseGraphicsTest)
        {
            // 注意：不开 Entry，所以 Entry.DoOnMainThread / AddFunc 在测试期间不可用。
            await GT.Main();
            return;
        }

        Func<CancellationToken, string[], Task> main = new(
            async (_, args) => await SimpleGameMain.Main(args)
        );
        await Entry.Run(main, args);
    }
}
