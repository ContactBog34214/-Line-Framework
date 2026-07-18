#nullable disable

using ManagedBass;
using ManagedBass.Fx;

namespace Line.Framework.Resource.Audio
{
    /// <summary>
    /// 音频控制器接口（对应单个音频资源）
    /// </summary>
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

    /// <summary>
    /// Bass 全局管理器（支持多设备初始化）
    /// </summary>
    internal static class BassManager
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static readonly HashSet<int> _initializedDevices = new HashSet<int>(); // 已初始化的设备索引
        private static readonly List<WeakReference<TAudio>> _audioInstances = new();

        /// <summary>
        /// 初始化默认设备（索引 -1）
        /// </summary>
        public static void Init()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;
                if (!Bass.Init())
                    throw new InvalidOperationException($"Bass init failed {Bass.LastError}");
                _initializedDevices.Add(-1);
                _initialized = true;
                Log.Debug("Bass inited");
            }
        }

        /// <summary>
        /// 确保指定设备已初始化
        /// </summary>
        public static void EnsureDeviceInitialized(int deviceIndex)
        {
            lock (_lock)
            {
                if (_initializedDevices.Contains(deviceIndex))
                    return;

                // 如果设备索引不是 -1，需要单独初始化
                if (deviceIndex == -1)
                {
                    // 默认设备应该在 Init() 中已初始化
                    if (!_initialized)
                        Init();
                    return;
                }

                // 检查设备是否存在
                if (!Bass.GetDeviceInfo(deviceIndex, out var info) || !info.IsEnabled)
                    throw new InvalidOperationException($"Device {deviceIndex} is not available");

                // 尝试初始化该设备
                if (!Bass.Init(deviceIndex))
                    throw new InvalidOperationException($"Bass init failed {Bass.LastError}");

                _initializedDevices.Add(deviceIndex);
                Log.Debug($"Bass inited");
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
            }
        }

        public static void Unregister(TAudio instance)
        {
            lock (_lock)
            {
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out var t) || t == instance);
            }
        }

        /// <summary>
        /// 释放 Bass（在所有 TAudio 实例释放后调用）
        /// </summary>
        public static void Free()
        {
            lock (_lock)
            {
                if (!_initialized)
                    return;
                var instances = new List<TAudio>();
                _audioInstances.RemoveAll(wr => !wr.TryGetTarget(out _));
                foreach (var wr in _audioInstances)
                    if (wr.TryGetTarget(out var inst))
                        instances.Add(inst);
                foreach (var inst in instances)
                    inst.Dispose();

                _audioInstances.Clear();
                Bass.Free();
                _initializedDevices.Clear();
                _initialized = false;
                Log.Debug("Bass released");
            }
        }
    }

    /// <summary>
    /// 音频设备查询辅助
    /// </summary>
    public static class AudioDevices
    {
        public static List<DeviceInfo> GetAllDevices()
        {
            var devices = new List<DeviceInfo>();
            if (Bass.GetDeviceInfo(-1, out var defaultInfo))
                devices.Add(defaultInfo);
            int i = 0;
            while (true)
            {
                if (Bass.GetDeviceInfo(i, out var info))
                {
                    if (i != -1)
                        devices.Add(info);
                }
                else
                    break;
                i++;
            }
            return devices;
        }

        public static DeviceInfo GetDeviceInfo(int deviceIndex)
        {
            if (Bass.GetDeviceInfo(deviceIndex, out var info))
                return info;
            return default;
        }
    }

    /// <summary>
    /// 音频资源类型，管理一组音频资源并提供全局属性和独立设备
    /// </summary>
    public class TAudio : ResourceType, IDisposable
    {
        private readonly Dictionary<string, AudioResource> _resources = new();
        private readonly object _lock = new object();
        private float _masterVolume = 1.0f;
        private float _masterSpeed = 1.0f;
        private float _masterPitch = 0f;
        private int _deviceIndex = -1;
        private bool _disposed = false;

        public TAudio(ResourceManager manager, int deviceIndex = -1)
            : base(manager)
        {
            BassManager.Init();
            BassManager.Register(this);
            _deviceIndex = deviceIndex;
            // 确保初始设备已初始化
            BassManager.EnsureDeviceInitialized(_deviceIndex);
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
                        res.ApplyAllAttributes();
                }
            }
        }

        public float MasterSpeed
        {
            get => _masterSpeed;
            set
            {
                lock (_lock)
                {
                    _masterSpeed = value > 0 ? value : 0.1f;
                    foreach (var res in _resources.Values)
                        res.ApplyAllAttributes();
                }
            }
        }

        public float MasterPitch
        {
            get => _masterPitch;
            set
            {
                lock (_lock)
                {
                    _masterPitch = value;
                    foreach (var res in _resources.Values)
                        res.ApplyAllAttributes();
                }
            }
        }

        public int DeviceIndex
        {
            get => _deviceIndex;
            set
            {
                lock (_lock)
                {
                    if (_deviceIndex == value)
                        return;
                    // 确保目标设备已初始化
                    BassManager.EnsureDeviceInitialized(value);
                    _deviceIndex = value;
                    foreach (var res in _resources.Values)
                        res.ApplyDevice();
                }
            }
        }

        public override void Create(string id, Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            lock (_lock)
            {
                var resource = new AudioResource(stream, this);
                _resources.Add(id, resource);
                Manager.AddResource(id, resource);
            }
        }

        internal void RemoveResourceFromManager(string id)
        {
            Manager.DisposeResource(id);
            lock (_lock)
            {
                _resources.Remove(id);
            }
        }

        internal void SaveStateAndRelease()
        {
            lock (_lock)
            {
                foreach (var res in _resources.Values)
                    res.Release();
            }
        }

        internal void RestoreStateAndLoad()
        {
            lock (_lock)
            {
                foreach (var res in _resources.Values)
                    res.Load();
                foreach (var res in _resources.Values)
                    res.ApplyAllAttributes();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                foreach (var id in _resources.Keys.ToList())
                    Manager.DisposeResource(id);
                _resources.Clear();
                BassManager.Unregister(this);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public static class Device
        {
            public static List<DeviceInfo> GetAllDevices() => AudioDevices.GetAllDevices();

            public static DeviceInfo GetDeviceInfo(int deviceIndex) =>
                AudioDevices.GetDeviceInfo(deviceIndex);

            public static void EnsureInit() => BassManager.Init();
        }
    }

    /// <summary>
    /// 单个音频资源
    /// </summary>
    public class AudioResource : IResource, IAudioController
    {
        private readonly TAudio _owner;
        private string _tempFilePath;
        private int _sourceStream;
        private int _tempoStream;
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
                    if (_loaded)
                        ApplyAllAttributes();
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
                    _speed = value > 0 ? value : 0.1f;
                    if (_loaded)
                        ApplyAllAttributes();
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
                    _pitch = value;
                    if (_loaded)
                        ApplyAllAttributes();
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
                throw new ArgumentException("Stream cannot be read", nameof(inputStream));

            string ext = ".tmp";
            if (inputStream is FileStream fs && !string.IsNullOrEmpty(fs.Name))
                ext = Path.GetExtension(fs.Name) ?? ".tmp";
            _tempFilePath = Path.GetRandomFileName() + ext;
            using (var file = File.Create(_tempFilePath))
                inputStream.CopyTo(file);
        }

        public void Play()
        {
            lock (_lock)
            {
                if (!_loaded)
                    Load();
                if (_tempoStream != 0)
                {
                    if (!Bass.ChannelPlay(_tempoStream))
                        Log.Warning($"Cannot play the audio ,code:{Bass.LastError}");
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
                if (_loaded)
                    return;
                if (string.IsNullOrEmpty(_tempFilePath) || !File.Exists(_tempFilePath))
                    throw new FileNotFoundException($"File {_tempFilePath} not found");

                _sourceStream = Bass.CreateStream(_tempFilePath, 0, 0, BassFlags.Decode);
                if (_sourceStream == 0)
                {
                    Log.Error($"Cannot create decode stream ,code: {Bass.LastError}");
                    throw new InvalidOperationException(
                        $"Cannot create decode stream ,code: {Bass.LastError}"
                    );
                }

                _tempoStream = BassFx.TempoCreate(_sourceStream, BassFlags.Default);
                if (_tempoStream == 0)
                {
                    Log.Error($"Cannot create Tempo stream ,code: {Bass.LastError}");
                    Bass.StreamFree(_sourceStream);
                    _sourceStream = 0;
                    throw new InvalidOperationException(
                        $"Cannot create Tempo stream ,code: {Bass.LastError}"
                    );
                }

                if (_savedPosition > 0)
                    Bass.ChannelSetPosition(_tempoStream, _savedPosition);

                // 应用设备
                ApplyDevice();

                ApplyAllAttributes();

                _loaded = true;
                Log.Debug($"Loaded,Tempo handle: {_tempoStream}");
            }
        }

        public object GetHandle()
        {
            lock (_lock)
            {
                return this;
            }
        }

        public void Release()
        {
            lock (_lock)
            {
                if (!_loaded)
                    return;
                if (IsPlaying)
                    return;

                long pos = Bass.ChannelGetPosition(_tempoStream);
                if (pos >= 0)
                    _savedPosition = pos;

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
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_loaded)
                {
                    long pos = Bass.ChannelGetPosition(_tempoStream);
                    if (pos >= 0)
                        _savedPosition = pos;

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
                }

                if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
                {
                    try
                    {
                        File.Delete(_tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Cannot delete the tmp file:{ex.Message}");
                    }
                    _tempFilePath = null;
                }
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 应用设备（确保设备已初始化，然后设置通道设备）
        /// </summary>
        internal void ApplyDevice()
        {
            if (!_loaded || _tempoStream == 0)
                return;
            int targetDevice = _owner.DeviceIndex;
            // 确保目标设备已初始化（BassManager 会处理）
            BassManager.EnsureDeviceInitialized(targetDevice);
            if (!Bass.ChannelSetDevice(_tempoStream, targetDevice))
                Log.Warning($"Cannot change device of {targetDevice} Code:{Bass.LastError}");
        }

        /// <summary>
        /// 应用所有属性（音量、速度、音高）
        /// </summary>
        internal void ApplyAllAttributes()
        {
            if (!_loaded || _tempoStream == 0)
                return;

            float finalVol = _owner.MasterVolume * _volume;
            Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Volume, finalVol);

            float finalSpeed = _owner.MasterSpeed * _speed;
            float tempoPercent = (finalSpeed - 1f) * 100f;
            Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Tempo, tempoPercent);

            float finalPitch = _owner.MasterPitch + _pitch;
            Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Pitch, finalPitch);

            // 确保设备设置（可能在属性变化后设备被重置，但一般无需重复调用）
            // 但为了保证一致性，仍调用一次（但可能影响性能，可选择性调用）
            // 此处不重复调用 ApplyDevice，因为设备一般不随属性变化。
        }

        // ----- 自然倍速辅助（静态方法） -----
        /// <summary>
        /// 根据速度倍率计算自然音高（半音），公式：Pitch = 12 * log2(Speed)
        /// </summary>
        public static float SpeedToPitch(float speed)
        {
            if (speed <= 0)
                return 0;
            return 12f * (float)Math.Log2(speed);
        }

        /// <summary>
        /// 根据音高（半音）反推速度倍率，公式：Speed = 2^(Pitch/12)
        /// </summary>
        public static float PitchToSpeed(float pitch)
        {
            return (float)Math.Pow(2.0, pitch / 12.0);
        }

        /// <summary>
        /// 实例方法：设置自然倍速，同时调整音高到对应的自然音高
        /// </summary>
        public void SetNaturalSpeed(float speed)
        {
            if (speed <= 0)
                speed = 0.1f; // 避免无效值
            this.Speed = speed;
            this.Pitch = SpeedToPitch(speed);
        }
    }

    public static class ResourceManagerAudioExtensions
    {
        public static IAudioController GetAudioController(this ResourceManager manager, string id)
        {
            var obj = manager.GetResource(id);
            return obj as IAudioController;
        }
    }
}
