using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;

namespace Line.Framework.Audio
{
    public sealed class AudioThread : IDisposable
    {
        private readonly Thread thread;
        private readonly BlockingCollection<Action> taskQueue = new();
        private volatile bool running = true;
        private static bool resolverSet;

        public AudioThread()
        {
            thread = new Thread(Run) { Name = "AudioThread", IsBackground = true };
            thread.Start();
        }

        // 跨平台库解析器
        private static void EnsureBassResolver()
        {
            if (resolverSet)
                return;

            NativeLibrary.SetDllImportResolver(
                typeof(Bass).Assembly,
                (name, asm, path) =>
                {
                    if (
                        name == "bass"
                        || name == "libbass"
                        || name == "bass.dll"
                        || name == "libbass.so"
                        || name == "libbass.dylib"
                    )
                    {
                        string os =
                            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
                            : "osx";
                        string arch = RuntimeInformation.OSArchitecture switch
                        {
                            Architecture.X64 => "x86_64",
                            Architecture.X86 => "x86",
                            Architecture.Arm64 => "arm64",
                            _ => "x86_64",
                        };
                        string libFileName =
                            os == "win"
                                ? "bass.dll"
                                : (os == "osx" ? "libbass.dylib" : "libbass.so");
                        string fullPath = Path.Combine(
                            AppContext.BaseDirectory,
                            "runtimes",
                            os,
                            arch,
                            libFileName
                        );
                        if (File.Exists(fullPath))
                            return NativeLibrary.Load(fullPath);
                    }
                    return IntPtr.Zero;
                }
            );

            resolverSet = true;
        }

        private void Run()
        {
            EnsureBassResolver();
            if (!Bass.Init())
                throw new Exception("Failed to initialize BASS audio system.");

            foreach (var action in taskQueue.GetConsumingEnumerable())
            {
                if (!running)
                    break;
                action();
            }

            Bass.Free();
        }

        public void Post(Action action)
        {
            if (!running)
                return;
            taskQueue.Add(action);
        }

        public T PostSync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            Post(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task.GetAwaiter().GetResult();
        }

        // ---------- 设备管理（兼容旧版 ManagedBass）----------
        public List<DeviceInfo> GetDevices()
        {
            return PostSync(() =>
            {
                var list = new List<DeviceInfo>();
                // 使用 try-catch 尝试不同的 API
                try
                {
                    // 方法1：使用 Bass.DeviceCount 属性 + Bass.GetDeviceInfo
                    for (int i = 0; i < Bass.DeviceCount; i++)
                    {
                        var info = Bass.GetDeviceInfo(i);
                        if (info.IsEnabled)
                            list.Add(info);
                    }
                }
                catch
                {
                    // 方法2：如果上面的方法失败，尝试使用 Bass.GetDeviceInfos()（如果存在）
                    // 但这里简单处理：只返回一个空列表，不崩溃
                }
                return list;
            });
        }

        public bool SetCurrentDevice(int deviceIndex)
        {
            return PostSync(() =>
            {
                try
                {
                    // 检查设备索引有效性
                    if (deviceIndex < 0)
                        return false;
                    // 尝试设置 CurrentDevice，忽略返回值（因为没有返回值）
                    Bass.CurrentDevice = deviceIndex;
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public int GetCurrentDevice()
        {
            return PostSync(() =>
            {
                try
                {
                    return Bass.CurrentDevice;
                }
                catch
                {
                    return -1;
                }
            });
        }

        public void SwitchToDefaultDevice()
        {
            Post(() =>
            {
                try
                {
                    // 尝试获取默认设备索引
                    var devices = GetDevices();
                    var defaultDev = devices.FirstOrDefault(d => d.IsDefault);
                    if (defaultDev.IsEnabled)
                    {
                        // 通过遍历找到索引（因为 DeviceInfo 可能没有 Index 属性）
                        for (int i = 0; i < devices.Count; i++)
                        {
                            if (devices[i].Equals(defaultDev))
                            {
                                Bass.CurrentDevice = i;
                                break;
                            }
                        }
                    }
                    else if (devices.Count > 0)
                    {
                        Bass.CurrentDevice = 0;
                    }
                }
                catch
                {
                    // 忽略错误，保持原设备
                }
            });
        }

        public void Dispose()
        {
            running = false;
            taskQueue.CompleteAdding();
            thread.Join();
        }
    }
}
