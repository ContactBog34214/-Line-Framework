#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedBass;
using ManagedBass.Fx;
using Line.Framework;
using Line.Framework.Resource;

namespace Line.Framework.Resource.Audio
{
    public interface IAudioController
    {
        void Play();
        void Pause();
        void Stop();

        bool IsPlaying { get; }
        bool IsPaused { get; }
        bool IsStopped { get; }

        float Volume { get; set; }
        float Speed { get; set; }
        float Pitch { get; set; }

        long PositionBytes { get; set; }
        double PositionSeconds { get; set; }
        double DurationSeconds { get; }

        bool IsLoaded { get; }
    }

    internal static class BassManager
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static int _currentDevice = -1;
        private static readonly List<WeakReference<TAudio>> _audioInstances = new();

        public static void Init()
        {
            lock (_lock)
            {
                if (_initialized) return;
                if (!Bass.Init())
                    throw new InvalidOperationException($"Bass 初始化失败，错误码: {Bass.LastError}");
                _currentDevice = -1;
                _initialized = true;
                Log.Debug("[BassManager] Bass 初始化成功");
            }
        }

        public static void Register(TAudio instance)
        {
            lock (_lock)
            {
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out _));
                if (_audioInstances.Any(wr => wr.TryGetTarget(out var t) && t == instance))
                    return;
                _audioInstances.Add(new WeakReference<TAudio>(instance));
                Log.Debug("[BassManager] 注册 TAudio 实例");
            }
        }

        public static void Unregister(TAudio instance)
        {
            lock (_lock)
            {
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out var t) || t == instance);
                Log.Debug("[BassManager] 注销 TAudio 实例");
            }
        }

        public static void SwitchDevice(int deviceIndex)
        {
            lock (_lock)
            {
                if (!_initialized) throw new InvalidOperationException("Bass 未初始化");
                if (deviceIndex == _currentDevice) return;

                Log.Info($"[BassManager] 切换设备从 {_currentDevice} 到 {deviceIndex}");

                var instances = new List<TAudio>();
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in _audioInstances)
                    if (wr.TryGetTarget(out var inst))
                        instances.Add(inst);

                foreach (var inst in instances)
                    inst.SaveStateAndRelease();

                Bass.Free();

                if (!Bass.Init(deviceIndex))
                    throw new InvalidOperationException($"Bass 重新初始化失败（设备 {deviceIndex}），错误码: {Bass.LastError}");
                _currentDevice = deviceIndex;

                foreach (var inst in instances)
                    inst.RestoreStateAndLoad();

                Log.Info("[BassManager] 设备切换完成");
            }
        }

        public static int GetCurrentDevice() => Bass.CurrentDevice;

        public static void Free()
        {
            lock (_lock)
            {
                if (!_initialized) return;
                var instances = new List<TAudio>();
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in _audioInstances)
                    if (wr.TryGetTarget(out var inst))
                        instances.Add(inst);
                foreach (var inst in instances)
                    inst.Dispose();

                _audioInstances.Clear();
                Bass.Free();
                _initialized = false;
                Log.Debug("[BassManager] Bass 已释放");
            }
        }
    }

    public static class AudioDevices
    {
        public static List<DeviceInfo> GetAllDevices()
        {
            var devices = new List<DeviceInfo>();
            for (int i = 1; ; i++)
            {
                if (Bass.GetDeviceInfo(i, out var info))
                    devices.Add(info);
                else
                    break;
            }
            return devices;
        }

        public static DeviceInfo? GetDeviceInfo(int deviceIndex)
        {
            if (deviceIndex <= 0) return null;
            if (Bass.GetDeviceInfo(deviceIndex, out var info))
                return info;
            return null;
        }
    }

    public class TAudio : ResourceType, IDisposable
    {
        private readonly Dictionary<string, AudioResource> _resources = new();
        private readonly object _lock = new object();
        private float _masterVolume = 1.0f;
        private bool _disposed = false;

        public TAudio(ResourceManager manager) : base(manager)
        {
            BassManager.Init();
            BassManager.Register(this);
            Log.Debug("[TAudio] 实例已创建");
        }

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                lock (_lock)
                {
                    _masterVolume = Math.Clamp(value, 0f, 1f);
                    foreach (var res in _resources.Values)
                        res.ApplyVolume();
                }
            }
        }

        public override void Create(string id, Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            lock (_lock)
            {
                if (_resources.ContainsKey(id))
                    throw new InvalidOperationException($"资源 ID '{id}' 已存在");
                var resource = new AudioResource(stream, this);
                _resources.Add(id, resource);
                Manager.AddResource(id, resource);
                Log.Debug($"[TAudio] 创建音频资源: {id}");
            }
        }

        internal void RemoveResourceFromManager(string id)
        {
            Manager.DisposeResource(id);
            lock (_lock) { _resources.Remove(id); }
        }

        internal void SaveStateAndRelease()
        {
            lock (_lock)
            {
                foreach (var res in _resources.Values)
                    res.Release();
                Log.Debug("[TAudio] 保存状态并释放所有资源");
            }
        }

        internal void RestoreStateAndLoad()
        {
            lock (_lock)
            {
                foreach (var res in _resources.Values)
                    res.Load();
                Log.Debug("[TAudio] 重新加载所有资源");
            }
        }

        internal Dictionary<AudioResource, bool> CaptureAllStates()
        {
            lock (_lock)
            {
                var dict = new Dictionary<AudioResource, bool>();
                foreach (var res in _resources.Values)
                    dict[res] = res.IsPlaying;
                return dict;
            }
        }

        internal void RestorePlayStates(Dictionary<AudioResource, bool> states)
        {
            lock (_lock)
            {
                foreach (var kvp in states)
                {
                    var res = kvp.Key;
                    bool wasPlaying = kvp.Value;
                    if (wasPlaying && res.IsLoaded)
                    {
                        int handle = (int)res.GetHandle();
                        if (handle != 0)
                            Bass.ChannelPlay(handle);
                    }
                }
                Log.Debug("[TAudio] 恢复播放状态");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                foreach (var id in _resources.Keys.ToList())
                    Manager.DisposeResource(id);
                _resources.Clear();
                BassManager.Unregister(this);
                _disposed = true;
                Log.Debug("[TAudio] 实例已释放");
            }
            GC.SuppressFinalize(this);
        }
    }

    public class AudioResource : IResource, IAudioController
    {
        private readonly TAudio _owner;
        private string _tempFilePath;
        private int _sourceStream;   // 解码源流（BassFlags.Decode）
        private int _tempoStream;    // Tempo 流（可播放，不加 Decode）
        private long _savedPosition = 0;
        private float _volume = 1.0f;
        private float _speed = 1.0f;
        private float _pitch = 0f;
        private bool _loaded = false;
        private readonly object _lock = new object();

        public bool IsLoaded
        {
            get
            {
                lock (_lock)
                {
                    if (!_loaded || _tempoStream == 0)
                        return false;
                    return Bass.ChannelGetInfo(_tempoStream, out _);
                }
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                lock (_lock)
                {
                    _volume = Math.Clamp(value, 0f, 1f);
                    if (_loaded) ApplyVolume();
                }
            }
        }

        public float Speed
        {
            get => _speed;
            set
            {
                lock (_lock)
                {
                    float newSpeed = Math.Clamp(value, 0.1f, 10.0f);
                    if (Math.Abs(_speed - newSpeed) < 0.001f) return;
                    _speed = newSpeed;
                    if (_loaded && _tempoStream != 0)
                    {
                        if (!Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Tempo, _speed))
                            Log.Warning($"[AudioResource] 设置速度失败，错误码: {Bass.LastError}");
                    }
                }
            }
        }

        public float Pitch
        {
            get => _pitch;
            set
            {
                lock (_lock)
                {
                    float newPitch = Math.Clamp(value, -12f, 12f);
                    if (Math.Abs(_pitch - newPitch) < 0.001f) return;
                    _pitch = newPitch;
                    if (_loaded && _tempoStream != 0)
                    {
                        if (!Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Pitch, _pitch))
                            Log.Warning($"[AudioResource] 设置音高失败，错误码: {Bass.LastError}");
                    }
                }
            }
        }

        public long PositionBytes
        {
            get
            {
                lock (_lock)
                {
                    if (_loaded && _tempoStream != 0)
                        return Bass.ChannelGetPosition(_tempoStream);
                    return _savedPosition;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_loaded && _tempoStream != 0)
                        Bass.ChannelSetPosition(_tempoStream, value);
                    else
                        _savedPosition = value;
                }
            }
        }

        public double PositionSeconds
        {
            get
            {
                lock (_lock)
                {
                    if (_loaded && _tempoStream != 0)
                    {
                        long bytes = Bass.ChannelGetPosition(_tempoStream);
                        return Bass.ChannelBytes2Seconds(_tempoStream, bytes);
                    }
                    return 0;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_loaded && _tempoStream != 0)
                    {
                        long bytes = Bass.ChannelSeconds2Bytes(_tempoStream, value);
                        Bass.ChannelSetPosition(_tempoStream, bytes);
                    }
                }
            }
        }

        public double DurationSeconds
        {
            get
            {
                lock (_lock)
                {
                    if (_loaded && _tempoStream != 0)
                    {
                        long bytes = Bass.ChannelGetLength(_tempoStream);
                        return Bass.ChannelBytes2Seconds(_tempoStream, bytes);
                    }
                    return 0;
                }
            }
        }

        public bool IsPlaying
        {
            get
            {
                lock (_lock)
                {
                    if (!_loaded || _tempoStream == 0)
                        return false;
                    return Bass.ChannelIsActive(_tempoStream) == PlaybackState.Playing;
                }
            }
        }

        public bool IsPaused
        {
            get
            {
                lock (_lock)
                {
                    if (!_loaded || _tempoStream == 0)
                        return false;
                    return Bass.ChannelIsActive(_tempoStream) == PlaybackState.Paused;
                }
            }
        }

        public bool IsStopped
        {
            get
            {
                lock (_lock)
                {
                    if (!_loaded || _tempoStream == 0)
                        return true;
                    return Bass.ChannelIsActive(_tempoStream) == PlaybackState.Stopped;
                }
            }
        }

        public AudioResource(Stream inputStream, TAudio owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (!inputStream.CanRead)
                throw new ArgumentException("流不可读", nameof(inputStream));

            string ext = ".tmp";
            if (inputStream is FileStream fs && !string.IsNullOrEmpty(fs.Name))
                ext = Path.GetExtension(fs.Name) ?? ".tmp";
            _tempFilePath = Path.GetTempFileName() + ext;
            using (var file = File.Create(_tempFilePath))
                inputStream.CopyTo(file);

            //Log.Debug($"[AudioResource] 创建临时文件: {_tempFilePath}");
        }

        public void Play()
        {
            lock (_lock)
            {
                if (!_loaded) Load();
                if (_tempoStream != 0)
                {
                    if (!Bass.ChannelPlay(_tempoStream))
                        Log.Warning($"[AudioResource] 播放失败，错误码: {Bass.LastError}");
                }
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_loaded && _tempoStream != 0)
                    Bass.ChannelPause(_tempoStream);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_loaded && _tempoStream != 0)
                {
                    Bass.ChannelStop(_tempoStream);
                    Bass.ChannelSetPosition(_tempoStream, 0);
                    _savedPosition = 0;
                }
            }
        }

        public void Load()
        {
            lock (_lock)
            {
                if (_loaded) return;
                if (string.IsNullOrEmpty(_tempFilePath) || !File.Exists(_tempFilePath))
                    throw new InvalidOperationException("临时文件不存在");

                // 1. 创建解码源流（必须加 BassFlags.Decode）
                _sourceStream = Bass.CreateStream(_tempFilePath, 0, 0, BassFlags.Decode);
                if (_sourceStream == 0)
                {
                    Log.Error($"[AudioResource] 创建解码流失败，错误码: {Bass.LastError}");
                    throw new InvalidOperationException($"创建解码流失败，错误码: {Bass.LastError}");
                }

                // 2. 创建 Tempo 流（不加 Decode，不加 FxFreeSource，独立管理）
                _tempoStream = BassFx.TempoCreate(_sourceStream, BassFlags.Default);
                if (_tempoStream == 0)
                {
                    Log.Error($"[AudioResource] 创建 Tempo 流失败，错误码: {Bass.LastError}");
                    Bass.StreamFree(_sourceStream);
                    _sourceStream = 0;
                    throw new InvalidOperationException($"创建 Tempo 流失败，错误码: {Bass.LastError}");
                }

                // 3. 应用当前速度/音高
                if (!Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Tempo, _speed))
                    Log.Warning($"[AudioResource] 初始设置速度失败，错误码: {Bass.LastError}");
                if (!Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Pitch, _pitch))
                    Log.Warning($"[AudioResource] 初始设置音高失败，错误码: {Bass.LastError}");

                // 4. 恢复位置
                if (_savedPosition > 0)
                    Bass.ChannelSetPosition(_tempoStream, _savedPosition);

                ApplyVolume();
                _loaded = true;
                Log.Debug($"[AudioResource] 加载成功，Tempo 句柄: {_tempoStream}");
            }
        }

        public object GetHandle()
        {
            lock (_lock) { return this; }
        }

        public void Release()
        {
            lock (_lock)
            {
                if (!_loaded) return;
                if (IsPlaying) return; // 播放中不释放

                // 保存位置
                long pos = Bass.ChannelGetPosition(_tempoStream);
                if (pos >= 0)
                    _savedPosition = pos;

                // 释放 Tempo 流（不释放源流）
                if (_tempoStream != 0)
                {
                    Bass.StreamFree(_tempoStream);
                    _tempoStream = 0;
                }
                // 释放源流
                if (_sourceStream != 0)
                {
                    Bass.StreamFree(_sourceStream);
                    _sourceStream = 0;
                }
                _loaded = false;
                Log.Debug($"[AudioResource] 已释放，位置保存: {_savedPosition}");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_loaded) return;
                long pos = Bass.ChannelGetPosition(_tempoStream);
                if (pos >= 0) _savedPosition = pos;

                if (_tempoStream != 0)
                {
                    Bass.StreamFree(_tempoStream);
                    _tempoStream = 0;
                }
                if (_sourceStream != 0)
                {
                    Bass.StreamFree(_sourceStream);
                    _sourceStream = 0;
                }
                _loaded = false;

                if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
                {
                    try { File.Delete(_tempFilePath); }
                    catch (Exception ex) { Log.Warning($"[AudioResource] 删除临时文件失败: {ex.Message}"); }
                    _tempFilePath = null;
                }
                Log.Debug("[AudioResource] 已 Dispose");
            }
            GC.SuppressFinalize(this);
        }

        internal void ApplyVolume()
        {
            if (!_loaded || _tempoStream == 0) return;
            float finalVol = _owner.MasterVolume * _volume;
            Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Volume, finalVol);
        }
    }

    public static class ResourceManagerAudioExtensions
    {
        public static IAudioController? GetAudioController(this ResourceManager manager, string id)
        {
            var obj = manager.GetResource(id);
            return obj as IAudioController;
        }
    }
}