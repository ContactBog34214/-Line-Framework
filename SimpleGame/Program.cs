using Line.Framework.Audio;

var audio = new AudioManager();

try
{
    var track = audio.LoadTrack(
        "test",
        "/home/smellyfish/Documents/Projects/LineFramework/SimpleGame/assets/かめりあ(Camellia) - Hello (BPM) 2025.ogg"
    );
    track.Play();
}
catch
{
    Console.WriteLine(1);
}

var devices = audio.GetAudioDevices();
for (int i = 0; i < devices.Count; i++)
{
    var device = devices[i];
    Console.WriteLine($"{i}: {device.Name}");
    if (device.Name == "PulseAudio Sound Server")
    {
        audio.SwitchDevice(i); // ✅ 传入索引 i
        Console.WriteLine($"Switched to device {i}");
        break;
    }
}

Console.WriteLine("Playing... press Enter to stop");
Console.ReadLine();
audio.Shutdown();
