using System;
using System.Collections.Generic;
using ManagedBass;
using TagLib;

namespace Line.Framework.Audio
{
    public sealed class AudioManager : IDisposable
    {
        private readonly AudioThread audioThread;
        private readonly Dictionary<string, Track> tracks = new();
        private readonly object tracksLock = new();
        private bool disposed;

        // 用于保存切换设备前的 Track 状态
        private readonly List<SavedTrackInfo> savedTracks = new();

        public AudioManager()
        {
            audioThread = new AudioThread();
            audioThread.OnBeforeReinit += OnBeforeReinit;
            audioThread.OnAfterReinit  += OnAfterReinit;
        }

        // ---------- 事件处理 ----------
        private void OnBeforeReinit()
        {
            lock (tracksLock)
            {
                savedTracks.Clear();
                foreach (var kv in tracks)
                {
                    var track = kv.Value;
                    savedTracks.Add(new SavedTrackInfo
                    {
                        Name = kv.Key,
                        FilePath = track.FilePath,
                        Position = track.CurrentPosition,
                        Volume = track.Volume,
                        WasPlaying = track.IsPlaying
                    });
                    track.Stop();
                }
                tracks.Clear();
            }
        }

        private void OnAfterReinit(int newDeviceIndex)
        {
            lock (tracksLock)
            {
                foreach (var info in savedTracks)
                {
                    var newTrack = new Track(audioThread, info.FilePath);
                    newTrack.Volume = info.Volume;
                    newTrack.CurrentPosition = info.Position;
                    if (info.WasPlaying)
                        newTrack.Play();
                    tracks[info.Name] = newTrack;
                }
                savedTracks.Clear();
            }
        }

        // ---------- 公开 API ----------
        public Track LoadTrack(string name, string filePath)
        {
            var track = new Track(audioThread, filePath);
            lock (tracksLock)
            {
                tracks[name] = track;
            }
            return track;
        }

        public Track? GetTrack(string name)
        {
            lock (tracksLock)
            {
                tracks.TryGetValue(name, out var track);
                return track;
            }
        }

        public void UnloadTrack(string name)
        {
            lock (tracksLock)
            {
                if (tracks.TryGetValue(name, out var track))
                {
                    track.Dispose();
                    tracks.Remove(name);
                }
            }
        }

        public List<DeviceInfo> GetAudioDevices() => audioThread.GetDevices();

        public bool SwitchDevice(int deviceIndex) => audioThread.SetCurrentDevice(deviceIndex);

        public int CurrentDevice => audioThread.GetCurrentDevice();

        public void SwitchToDefaultDevice() => audioThread.SwitchToDefaultDevice();

        public void Shutdown()
        {
            if (disposed) return;
            disposed = true;
            lock (tracksLock)
            {
                foreach (var track in tracks.Values)
                    track.Dispose();
                tracks.Clear();
            }
            audioThread.Dispose();
        }

        public void Dispose() => Shutdown();

        // ---------- 工具方法 ----------
        public double GetAudioDuration(string filePath)
        {
            int stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode);
            if (stream == 0)
            {
                Console.WriteLine($"创建音频流失败: {filePath}, 错误代码: {Bass.LastError}");
                return -1;
            }
            try
            {
                long byteLength = Bass.ChannelGetLength(stream);
                if (byteLength == -1)
                {
                    Console.WriteLine($"获取音频长度失败: {filePath}, 错误代码: {Bass.LastError}");
                    return -1;
                }
                return Bass.ChannelBytes2Seconds(stream, byteLength);
            }
            finally
            {
                Bass.StreamFree(stream);
            }
        }

        public (string title, string artist, string album) GetMetadata(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                string title = !string.IsNullOrEmpty(file.Tag.Title) ? file.Tag.Title : System.IO.Path.GetFileNameWithoutExtension(filePath);
                string artist = !string.IsNullOrEmpty(file.Tag.FirstPerformer) ? file.Tag.FirstPerformer : "Unknown Artist";
                string album  = !string.IsNullOrEmpty(file.Tag.Album)      ? file.Tag.Album      : "Unknown Album";
                return (title, artist, album);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取元数据失败: {ex.Message}");
                return (System.IO.Path.GetFileNameWithoutExtension(filePath), "Unknown Artist", "Unknown Album");
            }
        }

        public Sample LoadSample(string filePath) => new Sample(filePath);
    }

    internal struct SavedTrackInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public double Position { get; set; }
        public float Volume { get; set; }
        public bool WasPlaying { get; set; }
    }
}