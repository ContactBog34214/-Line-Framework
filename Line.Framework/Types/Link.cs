namespace Line.Framework.Types;

/// <summary>
/// 动态值
/// </summary>
/// <typeparam name="T"></typeparam>
public class DynamicValue<T>
{
    Func<T> fc = () => default;
    public T Value
    {
        get
        {
            if (usingLambda)
                return fc();
            return val;
        }
    }
    T val = default;
    bool usingLambda = false;

    public DynamicValue(T value, bool readOnly = false)
    {
        SetValue(value);
        ReadOnly = readOnly;
    }

    public DynamicValue(Func<T> lambda, bool readOnly = false)
    {
        SetValueAsLambda(lambda);
        ReadOnly = readOnly;
    }

    public DynamicValue(bool readOnly = false)
    {
        SetValue(default);
        ReadOnly = readOnly;
    }

    /// <summary>
    /// 设置动态值
    /// </summary>
    /// <param name="值"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetValue(T value)
    {
        if (ReadOnly)
            throw new InvalidOperationException("This DynamicValue is read-only");
        val = value;
        usingLambda = false;
        OnChange?.Invoke(value);
    }

    /// <summary>
    /// 设置动态值为Lambda表达式
    /// </summary>
    /// <param name="Lambda表达式"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetValueAsLambda(Func<T> Lambda)
    {
        if (ReadOnly)
            throw new InvalidOperationException("This DynamicValue is read-only");
        usingLambda = true;
        if (Lambda == null)
        {
            fc = default;
        }
        else
            fc = Lambda;
        OnChange?.Invoke(Value ?? default);
    }

    /// <summary>
    /// 克隆动态值
    /// </summary>
    /// <returns>克隆后的动态值</returns>
    public DynamicValue<T> Clone()
    {
        bool readOnly = ReadOnly;
        return Clone(readOnly);
    }

    /// <summary>
    /// 克隆动态值
    /// </summary>
    /// <param name="只读"></param>
    /// <returns>克隆后的动态值</returns>
    public DynamicValue<T> Clone(bool readOnly)
    {
        if (usingLambda)
            return new(fc, readOnly);
        return new(val, readOnly);
    }

    /// <summary>
    /// 启用只读
    /// </summary>
    public bool ReadOnly { get; init; } = false;

    public static implicit operator T(DynamicValue<T> link) => link.Value;

    public static implicit operator DynamicValue<T>(T value) => new DynamicValue<T>(value);

    public static implicit operator DynamicValue<T>(Func<T> lambda) => new DynamicValue<T>(lambda);
    public event Action<T> OnChange;
}
