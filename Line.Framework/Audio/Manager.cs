using System;
using System.Collections.Generic;
using ManagedBass;

namespace Line.Framework.Audio
{
    // 注意：IAudioPlayable 接口已移到单独的文件，这里不要重复定义！

    public sealed class AudioManager : IDisposable
    {
        private readonly AudioThread audioThread;
        private readonly Dictionary<string, Track> tracks = new();
        private bool disposed;

        public AudioManager()
        {
            audioThread = new AudioThread();
        }

        public Track LoadTrack(string name, string filePath)
        {
            var track = new Track(audioThread, filePath);
            tracks[name] = track;
            return track;
        }

        public Track GetTrack(string name) => tracks.GetValueOrDefault(name);

        public void UnloadTrack(string name)
        {
            if (tracks.TryGetValue(name, out var track))
            {
                track.Dispose();
                tracks.Remove(name);
            }
        }

        public List<DeviceInfo> GetAudioDevices() => audioThread.GetDevices();

        public bool SwitchDevice(int deviceIndex) => audioThread.SetCurrentDevice(deviceIndex);

        public int CurrentDevice => audioThread.GetCurrentDevice();

        public void SwitchToDefaultDevice() => audioThread.SwitchToDefaultDevice();

        public void Shutdown()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (var track in tracks.Values)
                track.Dispose();
            tracks.Clear();
            audioThread.Dispose();
        }

        public void Dispose() => Shutdown();
    }
}
