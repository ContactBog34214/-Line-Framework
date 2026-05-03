using System;
using ManagedBass;

namespace Line.Framework.Audio
{
    public class Track : IAudioPlayable, IDisposable
    {
        private readonly AudioThread audioThread;
        private readonly string filePath;
        private int streamHandle;
        private float volume = 1.0f;
        private double position = 0.0;
        private bool wasPlaying = false;
        private bool needsRebuild = false;
        private bool disposed = false;

        internal Track(AudioThread thread, string path)
        {
            audioThread = thread;
            filePath = path;
            streamHandle = thread.PostSync(() => Bass.CreateStream(filePath));
            if (streamHandle == 0)
                throw new Exception($"Failed to load track: {filePath}");
        }

        /// <summary>播放音频（若设备丢失后已重建，则恢复位置和音量）</summary>
        public void Play()
        {
            audioThread.Post(() =>
            {
                if (needsRebuild)
                    RebuildStream();

                if (streamHandle != 0)
                {
                    Bass.ChannelPlay(streamHandle);
                    wasPlaying = true;
                }
            });
        }

        /// <summary>暂停音频</summary>
        public void Pause()
        {
            audioThread.Post(() =>
            {
                if (streamHandle != 0)
                {
                    Bass.ChannelPause(streamHandle);
                    wasPlaying = false;
                }
            });
        }

        /// <summary>停止音频（位置重置到开头）</summary>
        public void Stop()
        {
            audioThread.Post(() =>
            {
                if (streamHandle != 0)
                {
                    Bass.ChannelStop(streamHandle);
                    wasPlaying = false;
                    position = 0;
                }
            });
        }

        /// <summary>音量（0.0 ~ 1.0）</summary>
        public float Volume
        {
            get => volume;
            set
            {
                volume = Math.Clamp(value, 0f, 1f);
                audioThread.Post(() =>
                {
                    if (streamHandle != 0)
                        Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Volume, volume);
                });
            }
        }

        /// <summary>当前播放位置（秒）</summary>
        public double CurrentTime
        {
            get
            {
                if (needsRebuild)
                    return position;
                return audioThread.PostSync(() =>
                {
                    if (streamHandle != 0)
                        return Bass.ChannelBytes2Seconds(
                            streamHandle,
                            Bass.ChannelGetPosition(streamHandle)
                        );
                    return 0;
                });
            }
        }

        /// <summary>跳转到指定位置（秒）</summary>
        public void Seek(double seconds)
        {
            position = seconds;
            audioThread.Post(() =>
            {
                if (streamHandle != 0)
                    Bass.ChannelSetPosition(
                        streamHandle,
                        Bass.ChannelSeconds2Bytes(streamHandle, seconds)
                    );
            });
        }

        /// <summary>是否正在播放</summary>
        public bool IsPlaying =>
            audioThread.PostSync(() =>
            {
                if (needsRebuild || streamHandle == 0)
                    return false;
                return Bass.ChannelIsActive(streamHandle) == PlaybackState.Playing;
            });

        /// <summary>标记为需要重建（设备丢失时调用）</summary>
        internal void MarkForRebuild(bool wasActive)
        {
            needsRebuild = true;
            wasPlaying = wasActive;
            // 保存当前播放位置
            if (streamHandle != 0)
                position = Bass.ChannelBytes2Seconds(
                    streamHandle,
                    Bass.ChannelGetPosition(streamHandle)
                );
            // 释放旧流
            if (streamHandle != 0)
            {
                Bass.StreamFree(streamHandle);
                streamHandle = 0;
            }
        }

        private void RebuildStream()
        {
            if (!needsRebuild)
                return;

            int newHandle = Bass.CreateStream(filePath);
            if (newHandle == 0)
                throw new Exception($"Failed to reload track after device change: {filePath}");

            streamHandle = newHandle;
            // 恢复音量
            Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Volume, volume);
            // 恢复位置
            if (position > 0)
                Bass.ChannelSetPosition(
                    streamHandle,
                    Bass.ChannelSeconds2Bytes(streamHandle, position)
                );
            // 如果之前正在播放，则恢复播放
            if (wasPlaying)
                Bass.ChannelPlay(streamHandle);

            needsRebuild = false;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            audioThread.Post(() =>
            {
                if (streamHandle != 0)
                    Bass.StreamFree(streamHandle);
                streamHandle = 0;
            });
        }

        ~Track() => Dispose();
    }
}
