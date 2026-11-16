namespace Line.Framework.Graphics;

/// <summary>
/// 比较函数：深度测试与模板测试共用的比较方式。
/// <para>
/// 语义为 <c>新值 &lt;比较方式&gt; 旧值</c>；例如 <see cref="Less"/> 表示
/// 「新片元更近才通过」。抽象层目前没有深度附件，因此只有将来接深度缓冲才会用到。
/// </para>
/// </summary>
public enum CompareFunction
{
    /// <summary>永不通过。</summary>
    Never,

    /// <summary>新值小于旧值时通过（深度测试的常规选择）。</summary>
    Less,

    /// <summary>相等时通过。</summary>
    Equal,

    /// <summary>小于或等于时通过。</summary>
    LessOrEqual,

    /// <summary>大于时通过。</summary>
    Greater,

    /// <summary>不等时通过。</summary>
    NotEqual,

    /// <summary>大于或等于时通过。</summary>
    GreaterOrEqual,

    /// <summary>总是通过。</summary>
    Always,
}
