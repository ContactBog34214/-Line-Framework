using Line.Framework.Types;

namespace Line.Framework.Graphics;

/// <summary>
/// 着色器阶段对象。
/// <para>
/// 抽象层不区分「源码」与「已编译字节码」：<see cref="ShaderCreateInfo.Bytes"/>
/// 可以给 GLSL 源码（两个后端都会在工厂内编成 SPIR-V），也可以给现成的
/// SPIR-V 字节。因此同一份调用代码可以跨后端使用。
/// </para>
/// </summary>
public interface IShader : IDisposable, IName
{
    /// <summary>入口函数名（GLSL 通常为 <c>main</c>）。</summary>
    string EntryPoint { get; }

    /// <summary>本对象所属的阶段（恰好一个）。</summary>
    ShaderStage Stage { get; }
}
