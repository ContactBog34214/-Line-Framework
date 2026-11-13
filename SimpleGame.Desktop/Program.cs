using Line.Framework;
using LineFramework.GraphicsTest;

namespace SimpleGame.Desktop;

public static class Desktop
{
    public static async Task Main(string[] args)
    {
        Func<CancellationToken, string[], Task> main = //new(async (_, _) => await GT.Main());
        new(async (_, args) => await SimpleGameMain.Main(args));
        await Entry.Run(main, args);
    }
}
