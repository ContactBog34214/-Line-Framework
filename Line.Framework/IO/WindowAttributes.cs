namespace Line.Framework.IO;

/// <summary>
/// 一个窗口的属性表。
/// <para>
/// <b>线程模型</b>：内部用一把 <c>lock</c> 保护字典。属性访问频率是
/// 「用户改一次设置」，不是每帧，不值得上无锁结构；而
/// <c>Has + Get</c> 这种组合用无锁结构会给出错误的原子性假象。
/// </para>
/// <para>
/// <c>Changing</c> 事件在<b>锁外</b>触发，订阅者可以安全地再操作本表 ——
/// 否则「在处理器里 Set 另一个属性」就是重入死锁。
/// </para>
/// </summary>
public sealed class WindowAttributes
{
    private readonly struct Meta
    {
        public Meta(Type valueType, object defaultValue)
        {
            ValueType = valueType;
            DefaultValue = defaultValue;
        }

        public Type ValueType { get; }
        public object DefaultValue { get; }
    }

    /// <summary>
    /// 键 → (期望类型, 默认值)。
    /// <para>
    /// 期望类型是 <see cref="Set"/> 做类型校验的唯一依据 —— 给错类型是调用方 bug，
    /// 直接抛异常，不走「返回失败」那条路。
    /// </para>
    /// </summary>
    private static readonly Dictionary<WindowAttribute, Meta> Registry = new()
    {
        [WindowAttribute.ShowCursor] = new(typeof(bool), true),
        [WindowAttribute.RelativeMouse] = new(typeof(bool), false),
        [WindowAttribute.MouseSpeedScale] = new(typeof(float), 1f),
        [WindowAttribute.TextInput] = new(typeof(bool), false),
        [WindowAttribute.AlwaysOnTop] = new(typeof(bool), false),
        [WindowAttribute.Opacity] = new(typeof(float), 1f),
    };

    private readonly Lock gate = new();

    /// <summary>只记录被<b>显式设置</b>过的值；读不到就走注册表默认值。</summary>
    private readonly Dictionary<WindowAttribute, object> values = new();

    /// <summary>
    /// 值将被改变时触发（在写表之前）。把
    /// <see cref="WindowAttributeChangedEventArgs.Cancel"/> 置 true 可否决本次修改。
    /// </summary>
    public event Action<WindowAttributeChangedEventArgs> Changing;

    /// <summary>值已改变之后触发。<b>纯通知</b>，不能否决。</summary>
    public event Action<WindowAttribute, object, object> Changed;

    /// <summary>
    /// 设置属性值。传 <c>null</c> 等价于 <see cref="Reset"/>。
    /// </summary>
    /// <returns>
    /// <see cref="WindowAttributeSetResult"/>. 与旧值相同时也返回
    /// <see cref="WindowAttributeSetResult.Applied"/>，且不触发任何事件。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">未知的属性键。</exception>
    /// <exception cref="ArgumentException">值的类型与注册表不符。</exception>
    public WindowAttributeSetResult Set(WindowAttribute attribute, object value)
    {
        var meta = GetMeta(attribute);

        if (value is null)
        {
            Reset(attribute);
            return WindowAttributeSetResult.Reset;
        }

        if (value.GetType() != meta.ValueType)
            throw new ArgumentException(
                $"WindowAttribute.{attribute} expects {meta.ValueType.Name}, "
                    + $"got {value.GetType().Name}.",
                nameof(value)
            );

        var old = ReadOr(attribute, meta.DefaultValue);

        // 未变化：直接返回，不触发事件 —— 否则订阅者会被「设置成相同的值」刷屏。
        if (Equals(old, value))
            return WindowAttributeSetResult.Applied;

        // 锁外触发，允许订阅者在处理器里重入本表。
        var args = new WindowAttributeChangedEventArgs(attribute, old, value);
        Changing?.Invoke(args);
        if (args.Cancel)
            return WindowAttributeSetResult.Canceled;

        lock (gate)
            values[attribute] = value;

        Changed?.Invoke(attribute, old, value);
        return WindowAttributeSetResult.Applied;
    }

    /// <summary>读当前值；未显式设置过时返回注册表默认值。</summary>
    /// <exception cref="ArgumentOutOfRangeException">未知的属性键。</exception>
    public object Get(WindowAttribute attribute)
    {
        var meta = GetMeta(attribute);
        return ReadOr(attribute, meta.DefaultValue);
    }

    /// <summary>带类型的取值门面。</summary>
    public T Get<T>(WindowAttribute attribute, T defaultValue = default)
    {
        var raw = Get(attribute);
        return raw is T typed ? typed : defaultValue;
    }

    /// <summary>
    /// 是否被<b>显式设置过</b>。
    /// <para>
    /// 注意这不是「是否有值」—— 有默认值的属性在从未设置过时也返回 false。
    /// </para>
    /// </summary>
    public bool Has(WindowAttribute attribute)
    {
        lock (gate)
            return values.ContainsKey(attribute);
    }

    /// <summary>尝试读取<b>显式设置过</b>的值；未设置过返回 false 且不写默认值。</summary>
    public bool TryGet(WindowAttribute attribute, out object value)
    {
        lock (gate)
        {
            if (values.TryGetValue(attribute, out value))
                return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 恢复为注册表默认值（会走 <see cref="Changing"/>，可被否决）。
    /// </summary>
    public void Reset(WindowAttribute attribute)
    {
        var meta = GetMeta(attribute);
        var old = ReadOr(attribute, meta.DefaultValue);

        if (Equals(old, meta.DefaultValue))
        {
            lock (gate)
                values.Remove(attribute);
            return;
        }

        var args = new WindowAttributeChangedEventArgs(attribute, old, meta.DefaultValue);
        Changing?.Invoke(args);
        if (args.Cancel)
            return;

        lock (gate)
            values.Remove(attribute);

        Changed?.Invoke(attribute, old, meta.DefaultValue);
    }

    private object ReadOr(WindowAttribute attribute, object fallback)
    {
        lock (gate)
            return values.TryGetValue(attribute, out var v) ? v : fallback;
    }

    private static Meta GetMeta(WindowAttribute attribute)
    {
        if (!Registry.TryGetValue(attribute, out var meta))
            throw new ArgumentOutOfRangeException(
                nameof(attribute),
                attribute,
                "Unknown window attribute."
            );

        return meta;
    }

    // ---- 常用项的强类型门面 ----
    //
    // 调用点因此有编译期类型。放在本类而不是 IWindow 上：以后加属性不用动接口。

    public bool ShowCursor
    {
        get => Get(WindowAttribute.ShowCursor, true);
        set => Set(WindowAttribute.ShowCursor, value);
    }

    public bool RelativeMouse
    {
        get => Get(WindowAttribute.RelativeMouse, false);
        set => Set(WindowAttribute.RelativeMouse, value);
    }

    /// <summary>灵敏度倍率。<b>非正值会被忽略</b>（与旧 WindowType 的行为一致）。</summary>
    public float MouseSpeedScale
    {
        get => Get(WindowAttribute.MouseSpeedScale, 1f);
        set
        {
            if (value > 0)
                Set(WindowAttribute.MouseSpeedScale, value);
        }
    }

    public bool TextInput
    {
        get => Get(WindowAttribute.TextInput, false);
        set => Set(WindowAttribute.TextInput, value);
    }
}
