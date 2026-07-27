using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Line.Framework.IO;

public class FileManager
{
    private readonly Stopwatch Base = new();
    public string WorkDir { get; private set; }
    private ConcurrentDictionary<string, string> MapCache { get; } = new();
    private ConcurrentDictionary<string, HashCacheType> HashCache { get; } = new();

    private record HashCacheType(byte[] Data, long LastGetTime);

    internal string MapDir => Path.Combine(WorkDir, "Map");
    public bool CompressFile { get; set; } = true;
    public bool? AllowCache
    {
        get
        {
            if (field == null)
                return GlobalAllowCache;
            return field;
        }
        set;
    } = null;
    public static bool GlobalAllowCache { get; set; } = true;
    public ulong MaximumCacheSize { get; set; } = (long)1024 * 1024 * 512;
    public TimeSpan MaximumCacheAge { get; set; } = new(0, 1, 0);
    public long CacheTotalSize { get; private set; } = 0;
    private Thread cacheManagerThread;

    public void ForceClearCache()
    {
        MapCache.Clear();
        HashCache.Clear();
        CacheTotalSize = 0;
    }

    private void CacheCollectorControlor()
    {
        WeakReference<FileManager> fm = new(this);
        while (true)
        {
            Thread.Sleep(5000);
            if (fm.TryGetTarget(out _))
                CacheCollector();
            else
                return;
        }
    }

    public void CacheCollector()
    {
        long size = 0; //顺便算个大小
        long modifiedSize = CacheTotalSize;
        if (AllowCache ?? GlobalAllowCache)
        {
            var hashes = HashCache
                .OrderBy(c => c.Value.LastGetTime)
                .Select(kvp => kvp.Key)
                .ToArray();
            foreach (var i in hashes)
            {
                HashCache.TryGetValue(i, out var val);
                if (val == null)
                {
                    HashCache.Remove(i, out _);
                    continue;
                }
                if (
                    Base.ElapsedMilliseconds - val.LastGetTime > MaximumCacheAge.TotalMilliseconds
                    || modifiedSize > (long)MaximumCacheSize
                )
                {
                    HashCache.Remove(i, out var c);
                    modifiedSize -= c.Data.Length;
                    continue;
                }
                size += val.Data.Length;
            }
            CacheTotalSize = size;
        }
        else
        {
            HashCache.Clear();
            CacheTotalSize = 0;
        }
    }

    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var brotli = new BrotliStream(ms, CompressionLevel.Optimal))
        {
            brotli.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    public static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(input, CompressionMode.Decompress))
        {
            brotli.CopyTo(output);
        }
        return output.ToArray();
    }

    public async Task ClearMap()
    {
        MapCache.Clear();
    }

    public async Task<string> ReadAllTextAsync(string Path)
    {
        return Encoding.UTF8.GetString(await ReadAllByteAsyncForHash(await GetFileHash(Path)));
    }

    public async Task<Byte[]> ReadAllBytesAsync(string Path)
    {
        return await ReadAllByteAsyncForHash(await GetFileHash(Path));
    }

    public async Task<string> GetFileHash(string Path)
    {
        try
        {
            string realPath = FormatPath(Path);
            string m = System.IO.Path.Combine(MapDir, realPath);
            string Hash = "";
            if (!MapCache.TryGetValue(realPath, out Hash))
                Hash = await File.ReadAllTextAsync(m);
            MapCache.TryAdd(realPath, Hash);
            return Hash;
        }
        catch
        {
            return "";
        }
    }

    public string ReadAllText(string Path) =>
        Task.Run(() => ReadAllTextAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    public Byte[] ReadAllBytes(string Path) =>
        Task.Run(() => ReadAllBytesAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    internal async Task<byte[]> ReadAllByteAsyncForHash(string Hash)
    {
        if (Hash.Length != 64)
            throw new InvalidDataException($"{Hash} is not a Hash");
        if ((AllowCache ?? GlobalAllowCache) && HashCache.TryGetValue(Hash, out var hc))
        {
            HashCacheType modified = new(hc.Data, Base.ElapsedMilliseconds);
            HashCache.TryUpdate(Hash, modified, hc);
            return hc.Data;
        }

        string path = Path.Combine(WorkDir, GetHashPath(Hash));
        byte[] data = await File.ReadAllBytesAsync(Path.Combine(path, "Data"));
        if (File.Exists(Path.Combine(path, ".Comp")))
            try
            {
                data = Decompress(data);
            }
            catch (Exception ex)
            {
                throw new IOException($"Cannot decompress data", ex);
            }
        if (AllowCache ?? GlobalAllowCache)
        {
            HashCacheType modified = new(data, Base.ElapsedMilliseconds);
            HashCache.TryAdd(Hash, modified);
        }
        return data;
    }

    public static string FormatPath(string Path)
    {
        string[] path = Path.Split('/');
        List<string> result = [];
        foreach (var i in path)
        {
            if (i == "..")
            {
                if (result.Count > 0)
                    result.RemoveAt(result.Count - 1);
            }
            else if (i.Length != 0)
                result.Add(i);
        }
        return string.Join('/', result);
    }

    public static string GetHashPath(string Hash)
    {
        if (Hash.Length != 64)
            throw new InvalidDataException($"{Hash} is not Hash");
        return Path.Combine(Hash.Substring(0, 2), Hash.Substring(2, 2), Hash.Substring(4));
    }

    public async Task CreateFileAsync(string Path)
    {
        var f = FormatPath(Path);
        string m = System.IO.Path.Combine(MapDir, f);
        if (File.Exists(m))
            return;
        await File.WriteAllTextAsync(m, await WriteAllBytesAsyncForHash([], f));
    }

    public void CreateFile(string Path) =>
        Task.Run(() => CreateFileAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    public void CreateDirectory(string Path)
    {
        string m = System.IO.Path.Combine(MapDir, FormatPath(Path));
        Directory.CreateDirectory(m);
    }

    public async Task WriteAllTextAsync(string Path, string Text)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        await File.WriteAllTextAsync(
            path,
            await WriteAllBytesAsyncForHash(Encoding.UTF8.GetBytes(Text), Path)
        );
    }

    public void WriteAllText(string Path, string Text) =>
        Task.Run(() => WriteAllTextAsync(Path, Text))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public async Task WriteAllBytesAsync(string Path, byte[] Byte)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        await File.WriteAllTextAsync(path, await WriteAllBytesAsyncForHash(Byte, Path));
    }

    public void WriteAllBytes(string Path, byte[] Bytes) =>
        Task.Run(() => WriteAllBytesAsync(Path, Bytes))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public async Task DeleteFileAsync(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        var fh = await GetFileHash(Path);
        File.Delete(path);
        await RemoveDataRefToHash(Path, fh);
        await TryDeleteHash(fh);
    }

    public void DeleteFile(string Path) =>
        Task.Run(() => DeleteFileAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    public async Task DeleteDirectoryAsync(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        string[] files = await GetFilesAsync(path);
        string[] dirs = await GetDirectoriesAsync(path);
        foreach (var i in dirs)
        {
            await DeleteDirectoryAsync(i);
        }
        foreach (var i in files)
        {
            await DeleteFileAsync(i);
        }
        if (Path != "")
            Directory.Delete(System.IO.Path.Combine(MapDir, Path));
    }

    public void DeleteDirectory(string Path) =>
        Task.Run(() => DeleteDirectoryAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    public async Task<string[]> GetFilesAsync(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        string[] tg = Directory.GetFiles(path);
        for (int i = 0; i < tg.Length; i++)
        {
            tg[i] = System.IO.Path.GetRelativePath(MapDir, tg[i]);
        }
        return tg;
    }

    public string[] GetFiles(string Path) =>
        Task.Run(() => GetFilesAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    public async Task<string[]> GetDirectoriesAsync(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        string[] tg = Directory.GetDirectories(path);
        for (int i = 0; i < tg.Length; i++)
        {
            tg[i] = System.IO.Path.GetRelativePath(MapDir, tg[i]);
        }
        return tg;
    }

    public async Task RenameFileAsync(string Path, string Name)
    {
        Path = FormatPath(Path);
        string tg = FormatPath($"{Path}/../{Name}");
        await MoveFileAsync(Path, tg);
    }

    public async Task RenameDirectoryAsync(string Path, string Name)
    {
        Path = FormatPath(Path);
        string tg = FormatPath($"{Path}/../{Name}");
        await MoveDirectoryAsync(Path, tg);
    }

    public async Task MoveFileAsync(string Path, string TargetPath)
    {
        Path = FormatPath(Path);
        TargetPath = FormatPath(TargetPath);
        string path = System.IO.Path.Combine(MapDir, Path);
        string hash = await GetFileHash(Path);
        string tgp = System.IO.Path.Combine(MapDir, TargetPath);
        await AddDataRefToHash(tgp, hash);
        File.Move(path, tgp);
        await RemoveDataRefToHash(path, hash);
    }

    public async Task CopyFileAsync(string Path, string TargetPath)
    {
        Path = FormatPath(Path);
        TargetPath = FormatPath(TargetPath);
        string path = System.IO.Path.Combine(MapDir, Path);
        string hash = await GetFileHash(Path);
        string tgp = System.IO.Path.Combine(MapDir, TargetPath);
        File.Copy(path, tgp);
        await AddDataRefToHash(tgp, hash);
    }

    public async Task CopyDirectoryAsync(string Path, string TargetPath)
    {
        Path = FormatPath(Path);
        TargetPath = FormatPath(TargetPath);
        string path = System.IO.Path.Combine(MapDir, Path);
        string tgp = System.IO.Path.Combine(MapDir, TargetPath);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException();
        if (System.IO.Path.Exists(tgp))
            throw new IOException($"{TargetPath} already exists.");
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(tgp)))
            throw new DirectoryNotFoundException();
        Directory.CreateDirectory(tgp);
        string[] dirs = await GetDirectoriesAsync(Path);
        string[] files = await GetFilesAsync(Path);
        foreach (var i in dirs)
        {
            await CopyDirectoryAsync(
                i,
                FormatPath($"{TargetPath}/{System.IO.Path.GetRelativePath(Path, i)}")
            );
        }
        foreach (var i in files)
        {
            await CopyFileAsync(
                i,
                FormatPath($"{TargetPath}/{System.IO.Path.GetRelativePath(Path, i)}")
            );
        }
    }

    public bool DirectoryExists(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        return Directory.Exists(path);
    }

    public bool FileExists(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        return File.Exists(path);
    }

    public bool PathExists(string Path)
    {
        Path = FormatPath(Path);
        string path = System.IO.Path.Combine(MapDir, Path);
        return System.IO.Path.Exists(path);
    }

    public async Task MoveDirectoryAsync(string Path, string TargetPath)
    {
        Path = FormatPath(Path);
        TargetPath = FormatPath(TargetPath);
        string path = System.IO.Path.Combine(MapDir, Path);
        string tgp = System.IO.Path.Combine(MapDir, TargetPath);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException();
        if (System.IO.Path.Exists(tgp))
            throw new IOException($"{TargetPath} already exists.");
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(tgp)))
            throw new DirectoryNotFoundException();
        Directory.CreateDirectory(tgp);
        string[] dirs = await GetDirectoriesAsync(Path);
        string[] files = await GetFilesAsync(Path);
        foreach (var i in dirs)
        {
            await MoveDirectoryAsync(
                i,
                FormatPath($"{TargetPath}/{System.IO.Path.GetRelativePath(Path, i)}")
            );
        }
        foreach (var i in files)
        {
            await MoveFileAsync(
                i,
                FormatPath($"{TargetPath}/{System.IO.Path.GetRelativePath(Path, i)}")
            );
        }
        Directory.Delete(path, true);
    }

    public void MoveDirectory(string Path, string Target) =>
        Task.Run(() => MoveDirectoryAsync(Path, Target))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public void MoveFile(string Path, string Target) =>
        Task.Run(() => MoveFileAsync(Path, Target)).ConfigureAwait(false).GetAwaiter().GetResult();

    public void CopyFile(string Path, string Target) =>
        Task.Run(() => CopyFileAsync(Path, Target)).ConfigureAwait(false).GetAwaiter().GetResult();

    public void CopyDirectory(string Path, string Target) =>
        Task.Run(() => CopyDirectoryAsync(Path, Target))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public void RenameFile(string Path, string Name) =>
        Task.Run(() => RenameFileAsync(Path, Name)).ConfigureAwait(false).GetAwaiter().GetResult();

    public void RenameDirectory(string Path, string Name) =>
        Task.Run(() => RenameDirectoryAsync(Path, Name))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

    public string[] GetDirectories(string Path) =>
        Task.Run(() => GetDirectoriesAsync(Path)).ConfigureAwait(false).GetAwaiter().GetResult();

    internal async Task<string> WriteAllBytesAsyncForHash(byte[] Byte, string Path = null)
    {
        Path = FormatPath(Path);
        string TextHash = ComputeSha256Hash(Byte);
        if (Path != null)
        {
            string FileHash = await GetFileHash(Path);
            if (FileHash != TextHash)
            {
                await RemoveDataRefToHash(Path, FileHash);
                await TryDeleteHash(FileHash);
                MapCache.Remove(Path, out _);
            }
        }
        var HashPath = System.IO.Path.Combine(WorkDir, GetHashPath(TextHash));
        if (!Directory.Exists(HashPath))
        {
            Directory.CreateDirectory(HashPath);
            var hashFilePath = System.IO.Path.Combine(HashPath, "Data");
            var hashRefPath = System.IO.Path.Combine(HashPath, "Refs");

            if (CompressFile)
            {
                Byte = Compress(Byte);
                await File.WriteAllBytesAsync(
                    System.IO.Path.Combine(HashPath, ".Comp"),
                    Array.Empty<byte>()
                );
            }
            Task t = File.WriteAllBytesAsync(hashFilePath, Byte);
            await File.WriteAllBytesAsync(hashRefPath, Array.Empty<byte>());
            await t;
        }
        if (Path != null)
            await AddDataRefToHash(Path, TextHash);
        HashCache.TryGetValue(TextHash, out var hc);
        HashCacheType modified = new(Byte, Base.ElapsedMilliseconds);
        if (AllowCache ?? GlobalAllowCache)
            if (hc != null)
                HashCache.TryUpdate(TextHash, modified, hc);
            else
                HashCache.TryAdd(TextHash, modified);
        return TextHash;
    }

    private FileManager() { }

    public FileManager(string WorkDir)
    {
        this.WorkDir = WorkDir;
        Directory.CreateDirectory(Path.Combine(WorkDir, "Map"));
        Base.Start();
        cacheManagerThread = new(CacheCollectorControlor);
        cacheManagerThread.Start();
    }

    public async Task<bool> TryDeleteHash(string Hash)
    {
        if (Hash.Length != 64)
            return false;
        var HashPath = GetHashPath(Hash);
        HashPath = Path.Combine(WorkDir, HashPath);
        if (Directory.Exists(HashPath))
        {
            var hashRefPath = Path.Combine(HashPath, "Refs");
            if (File.Exists(hashRefPath))
            {
                List<string> refs = (await File.ReadAllLinesAsync(hashRefPath)).ToList();
                bool Modified = false;
                bool Ref = false;
                for (int i = 0; i < refs.Count; i++)
                {
                    var item = refs[i];
                    if (
                        !File.Exists(Path.Combine(MapDir, item))
                        || (await GetFileHash(item)) != Hash
                    )
                    {
                        refs.RemoveAt(i);
                        i--;
                        Modified = true;
                        continue;
                    }
                    Ref = true;
                }
                if (Modified)
                    await File.WriteAllLinesAsync(hashRefPath, refs);
                if (!Ref)
                    Directory.Delete(HashPath, true);
                return !Ref;
            }
        }
        return false;
    }

    internal async Task AddDataRefToHash(string Path, string TargetHash)
    {
        if (Path != null)
        {
            Path = FormatPath(Path);
            string path = System.IO.Path.Combine(WorkDir, GetHashPath(TargetHash), "Refs");
            List<string> refs = (await File.ReadAllLinesAsync(path)).ToList();
            if (refs.Contains(Path))
                return;
            refs.Add(Path);
            await File.WriteAllLinesAsync(path, refs);
        }
    }

    internal async Task RemoveDataRefToHash(string Path, string TargetHash)
    {
        if (TargetHash.Length == 64)
            if (Path != null)
            {
                Path = FormatPath(Path);
                string path = System.IO.Path.Combine(WorkDir, GetHashPath(TargetHash), "Refs");
                List<string> refs = (await File.ReadAllLinesAsync(path)).ToList();
                if (!refs.Contains(Path))
                    return;
                refs.Remove(Path);
                await File.WriteAllLinesAsync(path, refs);
            }
    }

    public static string ComputeSha256Hash(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(inputBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeSha256Hash(byte[] input)
    {
        byte[] hashBytes = SHA256.HashData(input);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
