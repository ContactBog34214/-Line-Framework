using Line.Framework;

namespace SimpleGame.Desktop;

public static class Desktop
{
    public static async Task Main(string[] args)
    {
        Func<CancellationToken, string[], Task> main = new(async (_, args) => await SimpleGameMain.Main(args));
        await Entry.Run(main, args);
    }
}