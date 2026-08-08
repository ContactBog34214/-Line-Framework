using System.Collections.ObjectModel;
using System.Text.Json;

namespace Line.Framework;

public class Localization
{
    private readonly List<string> id = [];
    public ReadOnlyCollection<string> LoadedLanguage => id.AsReadOnly();
    private readonly Dictionary<string, Dictionary<string, string>> sources = [];
    private readonly object _lock = new();

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

    public void RemoveLanguage(string ID)
    {
        lock (_lock)
        {
            sources.Remove(ID);
            id.Remove(ID);
        }
    }

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

    public void ClearLanguage()
    {
        lock (_lock)
        {
            id.Clear();
            sources.Clear();
        }
    }

    public bool EnableFullExceptionOutput { get; set; } = false;
}
