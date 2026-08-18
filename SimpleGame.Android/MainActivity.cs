using System.Threading.Tasks;
using Android.App;
using Line.Framework.Default.IO;
using Org.Libsdl.App;
using SimpleGame;

namespace SimpleGame.Android;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : SDLActivity
{
    protected override void Main()
    {
        //SDL3.SDL.Init(SDL3.SDL.InitFlags.Video);
        Task.Run(async () => await SimpleGameMain.Main([$"--AndroidActity"]));
    }
}