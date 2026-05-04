using System;
using System.Collections.Generic;
using ManagedBass;

namespace Line.Framework.Audio
{
    public sealed class AudioThread : IDisposable
    {
        private bool initialized = false;
        private int currentDevice = -1;

        // 事件：使用明确且唯一的名称
        public event Action? OnBeforeReinit;
        public event Action<int>? OnAfterReinit;

        public AudioThread()
        {
            Initialize(-1);
        }

        private void Initialize(int deviceIndex)
        {
            if (initialized)
                Bass.Free();

            if (!Bass.Init(deviceIndex))
            {
                // 如果指定设备失败，回退到默认设备
                if (deviceIndex != -1 && Bass.Init(-1))
                    deviceIndex = -1;
                else
                    throw new InvalidOperationException($"Bass.Init failed, error: {Bass.LastError}");
            }

            initialized = true;
            currentDevice = Bass.CurrentDevice;
        }

        public List<DeviceInfo> GetDevices()
        {
            var devices = new List<DeviceInfo>();
            int count = Bass.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var info = Bass.GetDeviceInfo(i);
                if (info.IsEnabled)
                    devices.Add(info);
            }
            return devices;
        }

        public bool SetCurrentDevice(int deviceIndex)
        {
            if (!initialized)
                return false;

            var devInfo = Bass.GetDeviceInfo(deviceIndex);
            if (!devInfo.IsEnabled)
                return false;

            if (deviceIndex == currentDevice)
                return true;

            return Reinit(deviceIndex);
        }

        private bool Reinit(int newDeviceIndex)
        {
            // 1. 通知外部保存所有 Track 状态
            OnBeforeReinit?.Invoke();

            // 2. 释放 BASS
            Bass.Free();
            initialized = false;

            // 3. 重新初始化新设备
            if (!Bass.Init(newDeviceIndex))
            {
                // 回滚：重新初始化旧设备
                Bass.Init(currentDevice);
                return false;
            }

            currentDevice = Bass.CurrentDevice;
            initialized = true;

            // 4. 通知外部重建所有 Track
            OnAfterReinit?.Invoke(currentDevice);
            return true;
        }

        public int GetCurrentDevice() => currentDevice;

        public void SwitchToDefaultDevice() => SetCurrentDevice(-1);

        internal int CreateStream(string filePath, BassFlags flags)
        {
            if (!initialized)
                throw new InvalidOperationException("AudioThread not initialized");
            return Bass.CreateStream(filePath, 0, 0, flags);
        }

        public void Dispose()
        {
            if (initialized)
            {
                Bass.Free();
                initialized = false;
            }
        }
    }
}