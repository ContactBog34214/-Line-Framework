using System.Reflection;

namespace Line.Framework;

public static class Properties
{
    public static Property[] GetObjectProperties(
        Object obj,
        BindingFlags flags = BindingFlags.Public | BindingFlags.Instance
    )
    {
        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties(flags);
        List<Property> Result = [];
        for (int i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            if (prop.GetIndexParameters().Length > 0)
                continue;
            try
            {
                Result.Add(new() { prop = prop, obj = obj });
            }
            catch (Exception ex)
            {
                Log.Warning($"{ex}");
            }
        }
        return Result.ToArray();
    }
}

public struct Property
{
    public string Name
    {
        get => prop.Name;
    }
    public string Type
    {
        get => prop.PropertyType.FullName;
    }
    internal PropertyInfo prop { get; init; }
    internal object obj { get; init; }
    public object Value
    {
        get => CanRead ? prop.GetValue(obj) : default;
        set
        {
            if (!CanWrite)
                throw new InvalidProgramException($"{Name} cannot is not written");
            if (prop.GetType().IsInstanceOfType(value))
            {
                prop.SetValue(obj, value);
            }
        }
    }

    public bool CanRead { get; }
    public bool CanWrite { get; }
}
