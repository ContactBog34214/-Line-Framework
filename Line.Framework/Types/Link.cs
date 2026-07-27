namespace Line.Framework.Types;

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

    public void SetValue(T value)
    {
        if (ReadOnly)
            throw new InvalidOperationException("This DynamicValue is read-only");
        val = value;
        usingLambda = false;
    }

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
    }

    public DynamicValue<T> Clone()
    {
        bool readOnly = ReadOnly;
        return Clone(readOnly);
    }

    public DynamicValue<T> Clone(bool readOnly)
    {
        if (usingLambda)
            return new(fc, readOnly);
        return new(val, readOnly);
    }

    public bool ReadOnly { get; init; } = false;

    public static implicit operator T(DynamicValue<T> link) => link.Value;

    public static implicit operator DynamicValue<T>(T value) => new DynamicValue<T>(value);

    public static implicit operator DynamicValue<T>(Func<T> lambda) => new DynamicValue<T>(lambda);
}
