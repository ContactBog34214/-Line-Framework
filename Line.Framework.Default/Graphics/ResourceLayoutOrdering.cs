using Line.Framework.Graphics;

namespace Line.Framework.Default.Graphics;

/// <summary>
/// 资源布局元素 → 后端槽位的<b>唯一</b>排序规则。
/// <para>
/// <b>为什么需要它</b>：SDL3 对 SPIR-V 有硬性的分组要求
///（见 SDL_CreateGPUShader 文档）：
/// sampler 必须排在最前，然后是 storage texture，然后是 storage buffer；
/// 组内 binding 从 0 连续编号。uniform buffer 单独占一个 set。
/// 而 Veldrid 直接按布局元素顺序展开 —— 如果两边各行其是，
/// 同一份布局在两个后端上会落到不同的槽位。
/// </para>
/// <para>
/// 因此把「分组顺序」收拢到这里，两个后端都调用它：
/// 抽象层的元素顺序在<b>组内</b>有意义（决定相对次序），
/// 在<b>组间</b>没有意义（由本规则重排）。调用方因此不必关心分组。
/// </para>
/// </summary>
internal static class ResourceLayoutOrdering
{
    /// <summary>
    /// 分组优先级。数值小的先绑定。
    /// <para>
    /// 顺序取自 SDL 的 SPIR-V 约定，uniform 单独一组、排在最后
    ///（在 SDL 里它本来就属于另一个 set）。
    /// </para>
    /// </summary>
    private static int GroupOf(ResourceKind kind) =>
        kind switch
        {
            // 采样器（纹理 + 采样器对）永远在最前
            ResourceKind.TextureSampler => 0,
            // 可写纹理
            ResourceKind.StorageTexture => 1,
            // storage buffer（只读与可写同组，保持它们之间的相对顺序）
            ResourceKind.StorageBufferReadOnly => 2,
            ResourceKind.StorageBufferReadWrite => 2,
            // uniform 独占一组
            ResourceKind.UniformBuffer => 3,
            _ => 4,
        };

    /// <summary>
    /// 返回按规范顺序排列的元素下标。
    /// <para>
    /// 组内保持原始顺序（稳定排序），所以「同一类资源按元素出现顺序编号」
    /// 这个直觉依然成立。
    /// </para>
    /// </summary>
    public static int[] CanonicalOrder(ReadOnlySpan<ResourceLayoutElement> elements)
    {
        var indices = new int[elements.Length];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        // 手写插入排序，不用 OrderBy：
        // elements 是 ReadOnlySpan（ref-like），不能被 lambda 捕获（CS9108）。
        // 元素数量很少（一个布局通常 1~4 个），插入排序的 O(n²) 无所谓，
        // 而且它天然稳定 —— 同组内保持原始出现顺序。
        for (int i = 1; i < indices.Length; i++)
        {
            int key = indices[i];
            int keyGroup = GroupOf(elements[key].Kind);
            int j = i - 1;

            // 严格大于才后移 → 相等时保持原序（稳定）。
            while (j >= 0 && GroupOf(elements[indices[j]].Kind) > keyGroup)
            {
                indices[j + 1] = indices[j];
                j--;
            }

            indices[j + 1] = key;
        }

        return indices;
    }

    /// <summary>
    /// 元素按规范顺序分组后，某种 <see cref="ShaderStage"/> 下各资源的 binding 编号。
    /// <para>
    /// 返回的数组与输入元素一一对应（<c>result[元素下标] = binding</c>）。
    /// 一个元素同时声明两个阶段时，它在两组里的编号恰好相同
    ///（各组的计数器独立，而分组顺序一致）。
    /// </para>
    /// </summary>
    public static int[] BindingWithinGroup(ReadOnlySpan<ResourceLayoutElement> elements)
    {
        var counters = new int[8];
        var result = new int[elements.Length];

        // 按规范顺序遍历，才能保证计数与最终绑定顺序一致。
        foreach (var i in CanonicalOrder(elements))
        {
            int g = GroupOf(elements[i].Kind);
            result[i] = counters[g]++;
        }

        return result;
    }

    /// <summary>
    /// 生成一份「元素 → 槽位」的可读描述，用于 Debug 日志。
    /// <para>
    /// 排版形如：<c>Transform(UniformBuffer)=set1/b0</c>。
    /// 两个后端都是同一套编号，唯一的差别是 set 号：
    /// Veldrid 恒为 0（一个 layout = 一个 descriptor set），
    /// SDL3 按阶段落在 0/2（其余资源）或 1/3（uniform）。
    /// </para>
    /// </summary>
    public static string Describe(ReadOnlySpan<ResourceLayoutElement> elements, bool sdl3)
    {
        var bindings = BindingWithinGroup(elements);
        var parts = new List<string>(elements.Length);

        foreach (var i in CanonicalOrder(elements))
        {
            var e = elements[i];
            var name = string.IsNullOrEmpty(e.Name) ? $"#{i}" : e.Name;

            if (!sdl3)
            {
                parts.Add($"{name}({e.Kind})=set0/b{bindings[i]}");
                continue;
            }

            // SDL3：uniform 单独成 set，按阶段落在 1（顶点）/ 3（片元）；
            // 其余资源落在 0（顶点）/ 2（片元）。这里两个阶段都列出来，
            // 便于一眼看出同一元素在不同阶段绑在哪。
            bool isUbo = e.Kind == ResourceKind.UniformBuffer;
            var sets = new List<string>(2);

            if ((e.Stages & ShaderStage.Vertex) != 0)
                sets.Add($"VS=set{(isUbo ? 1 : 0)}/b{bindings[i]}");
            if ((e.Stages & ShaderStage.Fragment) != 0)
                sets.Add($"FS=set{(isUbo ? 3 : 2)}/b{bindings[i]}");
            if (sets.Count == 0)
                sets.Add("(no stage)");

            parts.Add($"{name}({e.Kind})={string.Join(",", sets)}");
        }

        return string.Join("  ", parts);
    }
}
