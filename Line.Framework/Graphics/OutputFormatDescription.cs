namespace Line.Framework.Graphics
{
    /// <summary>
    /// 一组输出附件的格式描述，用于创建管线。
    /// <para>
    /// <b>获取方式</b>：从 <see cref="IRenderTarget.OutputFormat"/> 读取
    /// （交换链的渲染目标即 <see cref="ISwapchain.RenderTarget"/>）。
    /// 不要通过遍历 <see cref="IRenderTarget.ColorTextures"/> 自行拼装 ——
    /// SDL3 后端的 ColorTargets 是空数组，那样做会得到错误结果。
    /// </para>
    /// </summary>
    public struct OutputFormatDescription
    {
        /// <summary>深度附件格式；null 表示不使用深度缓冲（SDL3 恒为 null）。</summary>
        public OutputAttachmentDescription? DepthAttachment { get; set; }

        /// <summary>颜色附件格式清单，顺序即附件 index。</summary>
        public OutputAttachmentDescription[] ColorAttachments { get; set; }

        /// <summary>多重采样数，必须与帧缓冲实际使用的采样数一致。</summary>
        public SampleCount SampleCount { get; set; }
    }
}
