using System.Reflection;

namespace Line.Framework;

public static class Properties
{
    /// <summary>
    /// 获取对象所有属性
    /// </summary>
    /// <param name="目标对象"></param>
    /// <param name="标签"></param>
    /// <returns>属性数组</returns>
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
    /// <summary>
    /// 属性名
    /// </summary>
    public string Name
    {
        get => prop.Name;
    }

    /// <summary>
    /// 属性类型
    /// </summary>
    public string Type
    {
        get => prop.PropertyType.FullName;
    }
    internal PropertyInfo prop { get; init; }
    internal object obj { get; init; }

    /// <summary>
    /// 属性值
    /// </summary>
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

    /// <summary>
    /// 可读
    /// </summary>
    public bool CanRead { get; }

    /// <summary>
    /// 可写
    /// </summary>
    public bool CanWrite { get; }
}
