namespace Line.Framework.IO;

/// <summary>
/// 「属性即将改变」的参数。
/// <para>
/// 在值写进表<b>之前</b>触发，因此订阅者看到的是旧值仍在生效的状态 ——
/// 后端可以在这里尝试调用原生 API，失败就否决，表里不会留下「说了开了其实没开」的值。
/// </para>
/// </summary>
public sealed class WindowAttributeChangedEventArgs
{
    public WindowAttributeChangedEventArgs(
        WindowAttribute attribute,
        object oldValue,
        object newValue
    )
    {
        Attribute = attribute;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>被修改的属性。</summary>
    public WindowAttribute Attribute { get; }

    /// <summary>修改前的值（未显式设置过时是注册表默认值）。</summary>
    public object OldValue { get; }

    /// <summary>准备写入的值。</summary>
    public object NewValue { get; }

    /// <summary>
    /// 置 true 否决本次修改。表里的值保持不变。
    /// <para>
    /// 后端不支持某项、或原生调用失败时用它，<b>而不是抛异常</b> ——
    /// 在 Wayland 上设不了某个属性是正常情况，不是调用方的 bug。
    /// </para>
    /// </summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// <see cref="WindowAttributes.Set"/> 的结果。
/// <para>
/// 类型不匹配不在这里 —— 那是调用方 bug，直接抛 <see cref="ArgumentException"/>。
/// </para>
/// </summary>
public enum WindowAttributeSetResult
{
    /// <summary>已生效（含「与旧值相同，什么都没做」）。</summary>
    Applied,

    /// <summary>被 <see cref="WindowAttributes.Changing"/> 订阅者否决，值未改变。</summary>
    Canceled,

    /// <summary>已恢复为注册表默认值。</summary>
    Reset,
}
