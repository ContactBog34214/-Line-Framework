using System;
using ManagedBass;

namespace Line.Framework.Audio
{
    public sealed class Track : IDisposable
    {
        private readonly AudioThread audioThread;
        private readonly string filePath;
        private int streamHandle;
        private bool disposed = false;
        private float volume = 1.0f;
        private bool isPlaying = false;
        private float originalFrequency;

        internal int StreamHandle => streamHandle;
        public string FilePath => filePath;

        public float Volume
        {
            get => volume;
            set
            {
                volume = Math.Clamp(value, 0f, 1f);
                if (streamHandle != 0)
                    Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Volume, volume);
            }
        }

        public bool IsPlaying => isPlaying && streamHandle != 0 && Bass.ChannelIsActive(streamHandle) == PlaybackState.Playing;

        public double TotalSeconds { get; private set; }

        public double CurrentPosition
        {
            get
            {
                if (streamHandle == 0) return 0;
                long bytes = Bass.ChannelGetPosition(streamHandle);
                return Bass.ChannelBytes2Seconds(streamHandle, bytes);
            }
            set
            {
                if (streamHandle == 0) return;
                long bytes = Bass.ChannelSeconds2Bytes(streamHandle, Math.Max(0, value));
                Bass.ChannelSetPosition(streamHandle, bytes);
            }
        }

        internal Track(AudioThread thread, string path)
        {
            audioThread = thread ?? throw new ArgumentNullException(nameof(thread));
            filePath = path;

            streamHandle = audioThread.CreateStream(filePath, BassFlags.Default);
            if (streamHandle == 0)
                throw new InvalidOperationException($"Failed to create stream: {Bass.LastError}");

            // 获取原始采样率（用于变速变调）
            Bass.ChannelGetAttribute(streamHandle, ChannelAttribute.Frequency, out originalFrequency);
            if (originalFrequency <= 0) originalFrequency = 44100;

            long lengthBytes = Bass.ChannelGetLength(streamHandle);
            TotalSeconds = Bass.ChannelBytes2Seconds(streamHandle, lengthBytes);

            Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Volume, volume);
        }

        // ---------- 播放控制 ----------
        public void Play()
        {
            if (streamHandle == 0) return;
            if (!Bass.ChannelPlay(streamHandle))
                return;
            isPlaying = true;
        }

        public void Pause()
        {
            if (streamHandle == 0) return;
            Bass.ChannelPause(streamHandle);
            isPlaying = false;
        }

        public void Stop()
        {
            if (streamHandle == 0) return;
            Bass.ChannelStop(streamHandle);
            CurrentPosition = 0;
            isPlaying = false;
        }

        // ---------- 跳转时间 ----------
        public void SeekToSeconds(double seconds)
        {
            CurrentPosition = seconds;
        }

        // ---------- 变速变调（音调同时改变）----------
        public void SetPlaybackSpeed(float speed)
        {
            if (streamHandle == 0) return;
            float newFreq = originalFrequency * Math.Clamp(speed, 0.1f, 10.0f);
            Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Frequency, newFreq);
        }

        // ---------- 变速不变调（需要 BASS_FX）----------
        public void SetTempo(float tempoPercent)
        {
            if (streamHandle == 0) return;
            // tempoPercent: -95.0 ～ +5000.0，0 = 正常速度
            var attr = (ChannelAttribute)65536; // BASS_ATTRIB_TEMPO
            Bass.ChannelSetAttribute(streamHandle, attr, tempoPercent);
        }

        public void SetPitch(float pitch)
        {
            if (streamHandle == 0) return;
            var attr = (ChannelAttribute)65537; // BASS_ATTRIB_TEMPO_PITCH
            Bass.ChannelSetAttribute(streamHandle, attr, pitch);
        }

        public void SetTempoAndPitch(float tempoPercent, float pitch)
        {
            SetTempo(tempoPercent);
            SetPitch(pitch);
        }

        // ---------- 重置所有效果 ----------
        public void ResetEffects()
        {
            if (streamHandle == 0) return;
            // 重置频率
            Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Frequency, originalFrequency);
            // 重置 tempo 效果
            var tempoAttr = (ChannelAttribute)65536;
            Bass.ChannelSetAttribute(streamHandle, tempoAttr, 0f);
            // 重置 pitch
            var pitchAttr = (ChannelAttribute)65537;
            Bass.ChannelSetAttribute(streamHandle, pitchAttr, 0f);
        }

        // ---------- 设备迁移（切换输出设备时使用）----------
        public bool MoveToDevice(int newDeviceIndex)
        {
            if (streamHandle == 0) return false;

            bool wasPlaying = IsPlaying;
            double position = CurrentPosition;
            float vol = Volume;

            if (wasPlaying)
                Pause();

            bool success = Bass.ChannelSetDevice(streamHandle, newDeviceIndex);
            if (!success)
            {
                if (wasPlaying)
                    Play();
                return false;
            }

            Volume = vol;
            CurrentPosition = position;

            if (wasPlaying)
                Play();

            return true;
        }

        // ---------- 资源释放 ----------
        public void Dispose()
        {
            if (disposed) return;
            if (streamHandle != 0)
            {
                Bass.ChannelStop(streamHandle);
                Bass.StreamFree(streamHandle);
                streamHandle = 0;
            }
            disposed = true;
        }
    }
}