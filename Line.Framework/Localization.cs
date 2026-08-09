using System.Collections.ObjectModel;
using System.Text.Json;

namespace Line.Framework;

public class Localization
{
    private readonly List<string> id = [];

    /// <summary>
    /// 已加载的语言ID列表
    /// </summary>
    public ReadOnlyCollection<string> LoadedLanguage => id.AsReadOnly();
    private readonly Dictionary<string, Dictionary<string, string>> sources = [];
    private readonly object _lock = new();

    /// <summary>
    /// 设置语言
    /// </summary>
    /// <param name="语言ID"></param>
    /// <param name="语言json文本"></param>
    /// <param name="目标索引"></param>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public void SetLanguage(string ID, string json, int index = -1)
    {
        var strDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        lock (_lock)
        {
            if (sources.TryGetValue(ID, out var tmp))
            {
                sources[ID] = strDict;
                if (index != -1)
                {
                    if (index < 0 || index >= id.Count)
                        throw new IndexOutOfRangeException();
                    var IdxOf = id.IndexOf(ID);
                    if (IdxOf == index)
                        return;
                    id.RemoveAt(IdxOf);
                    id.Insert(index, ID);
                }
            }
            else
            {
                sources.TryAdd(ID, strDict);
                if (index != -1)
                {
                    if (index < 0 || index >= id.Count)
                        throw new IndexOutOfRangeException();
                    id.Insert(index, ID);
                    return;
                }
                id.Add(ID);
            }
        }
    }

    /// <summary>
    /// 移除语言
    /// </summary>
    /// <param name="目标语言ID"></param>
    public void RemoveLanguage(string ID)
    {
        lock (_lock)
        {
            sources.Remove(ID);
            id.Remove(ID);
        }
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="翻译键"></param>
    /// <param name="参数"></param>
    /// <returns></returns>
    public string Get(string Key, string[] Args = null)
    {
        string select = Key;
        Args = Args == null ? [] : Args;
        lock (_lock)
        {
            foreach (var i in id)
            {
                if (!sources.TryGetValue(i, out var json))
                {
                    continue;
                }
                if (!json.TryGetValue(Key, out var t))
                    continue;
                try
                {
                    select = string.Format(t, Args ?? []);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        $"Localization Error. Key:{Key},Args:[{string.Join("|", Args)}],LangId:{i}{(EnableFullExceptionOutput ? $"Full Exception:{ex}" : "")}"
                    );
                    select = Key;
                    continue;
                }
                break;
            }
            return select;
        }
    }

    /// <summary>
    /// 清除语言
    /// </summary>
    public void ClearLanguage()
    {
        lock (_lock)
        {
            id.Clear();
            sources.Clear();
        }
    }

    /// <summary>
    /// 是否启用完整错误输出
    /// </summary>
    public bool EnableFullExceptionOutput { get; set; } = false;
}
