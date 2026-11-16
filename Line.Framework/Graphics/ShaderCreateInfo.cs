namespace Line.Framework.Graphics;

/// <summary>
/// 创建着色器的参数。
/// <para>
/// <see cref="Bytes"/> 既可以是 GLSL 源码文本，也可以是编译好的 SPIR-V 字节；
/// 后端会按嗅探结果决定是否需要编译。这样同一份调用代码可以跨后端使用。
/// </para>
/// <para>
/// <b>如果传的是已编译的 SPIR-V</b>，SDL3 后端无法再改写 descriptor 的
/// set 编号，因此那份字节码必须按 SDL 的约定产出（顶点：资源 set 0 /
/// uniform set 1；片元：资源 set 2 / uniform set 3）。传 GLSL 源码时
/// 这些转换由后端自动完成。
/// </para>
/// </summary>
public struct ShaderCreateInfo
{
    /// <summary>
    /// 着色器阶段。<b>必须恰好指定一位</b>，否则赋值时就抛异常
    /// （区别于资源布局里可以按位或的用法）。
    /// </summary>
    public ShaderStage Stage
    {
        get;
        set
        {
            if (System.Numerics.BitOperations.PopCount((uint)value) != 1)
                throw new InvalidDataException($"ShaderStage cannot be {value}");
            field = value;
        }
    }

    /// <summary>GLSL 源码文本或 SPIR-V 字节。</summary>
    public byte[] Bytes { get; set; }

    /// <summary>入口函数名；留空时按 <c>main</c> 处理。</summary>
    public string EntryPoint { get; set; }

    /// <summary>
    /// 本阶段将会使用的资源布局。
    /// <para>
    /// <b>SDL3 必需</b>：该后端在创建着色器时就要知道用了几个 sampler /
    /// uniform / storage 资源，数量来自这里。漏传会被记成 0，
    /// 导致资源读取全部失败且不报错。
    /// </para>
    /// <para>
    /// Veldrid 不使用此字段（资源数量由资源布局与资源集决定）。
    /// 传 null 时 SDL3 会按「无资源」处理。
    /// </para>
    /// </summary>
    public IResourceLayout ResourceLayout { get; set; }
}
