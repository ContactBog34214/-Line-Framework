namespace Line.Framework.Default.Graphics;

/// <summary>
/// 从着色器字节里嗅探格式。
/// <para>
/// 抽象层没有「着色器语言」字段：SDL3 要求显式声明格式、
/// Veldrid 则要求拿到 SPIR-V 二进制，两者都要先判一次输入类型。
/// </para>
/// <para>
/// 本类放在 <c>Line.Framework.Default.Graphics</c> 而不是 SDL3 子命名空间，
/// 因为两个后端共用它（子命名空间里的代码可以直接用非限定名找到它）。
/// </para>
/// </summary>
public static class ShaderFormatSniffer
{
    /// <summary>可识别的着色器输入类型。</summary>
    public enum Kind
    {
        /// <summary>无法识别。</summary>
        Unknown,

        /// <summary>SPIR-V 二进制。</summary>
        SpirV,

        /// <summary>DXBC 容器（D3D11 字节码）。</summary>
        Dxbc,

        /// <summary>GLSL 源码文本。</summary>
        Glsl,

        /// <summary>Metal Shading Language 源码文本。</summary>
        Msl,
    }

    /// <summary>SPIR-V 魔数 0x07230203（小端字节序 03 02 23 07）。</summary>
    public static bool IsSpirV(ReadOnlySpan<byte> code) =>
        code.Length >= 4 && code[0] == 0x03 && code[1] == 0x02 && code[2] == 0x23 && code[3] == 0x07;

    /// <summary>DXBC 容器头。</summary>
    public static bool IsDxbc(ReadOnlySpan<byte> code) =>
        code.Length >= 4
        && code[0] == (byte)'D'
        && code[1] == (byte)'X'
        && code[2] == (byte)'B'
        && code[3] == (byte)'C';

    /// <summary>
    /// 跳过 UTF-8 BOM 与前导空白。
    /// <para>
    /// 必须跳过 —— C# 的逐字字符串（<c>@"..."</c>）常常以换行开头，
    /// 直接看首字节会把 <c>"\n#version 450"</c> 判成非文本。
    /// </para>
    /// </summary>
    private static ReadOnlySpan<byte> SkipLeading(ReadOnlySpan<byte> code)
    {
        int i = 0;

        // UTF-8 BOM
        if (code.Length >= 3 && code[0] == 0xEF && code[1] == 0xBB && code[2] == 0xBF)
            i = 3;

        while (
            i < code.Length
            && (code[i] == (byte)' '
                || code[i] == (byte)'\t'
                || code[i] == (byte)'\r'
                || code[i] == (byte)'\n')
        )
            i++;

        return code[i..];
    }

    private static bool StartsWith(ReadOnlySpan<byte> code, string ascii)
    {
        if (code.Length < ascii.Length)
            return false;

        for (int i = 0; i < ascii.Length; i++)
            if (code[i] != (byte)ascii[i])
                return false;

        return true;
    }

    /// <summary>
    /// GLSL 源码识别：跳过 BOM/空白后以 <c>#version</c>、
    /// <c>#extension</c> 或 <c>#ifdef</c> 等预处理指令开头。
    /// </summary>
    public static bool IsGlslSource(ReadOnlySpan<byte> code)
    {
        var s = SkipLeading(code);
        return StartsWith(s, "#version")
            || StartsWith(s, "#extension")
            || StartsWith(s, "#ifdef")
            || StartsWith(s, "#define")
            || StartsWith(s, "#pragma")
            || StartsWith(s, "//")
            || StartsWith(s, "/*");
    }

    /// <summary>
    /// 判断是否为可打印的文本（MSL 的粗略判据）。
    /// 只检查跳过前导空白后的第一个字节。
    /// </summary>
    public static bool LooksLikeText(ReadOnlySpan<byte> code)
    {
        var s = SkipLeading(code);
        return s.Length > 0 && s[0] >= 0x20 && s[0] < 0x7F;
    }

    /// <summary>按魔数/文本特征推断格式。</summary>
    public static Kind Detect(ReadOnlySpan<byte> code)
    {
        if (IsSpirV(code))
            return Kind.SpirV;
        if (IsDxbc(code))
            return Kind.Dxbc;
        if (IsGlslSource(code))
            return Kind.Glsl;
        if (LooksLikeText(code))
            return Kind.Msl;

        return Kind.Unknown;
    }
}
