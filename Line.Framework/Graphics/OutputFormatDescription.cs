namespace Line.Framework.Graphics
{
    public struct OutputFormatDescription
    {
        public OutputAttachmentDescription? DepthAttachment { get; set; }
        public OutputAttachmentDescription[] ColorAttachments { get; set; }
        public SampleCount SampleCount { get; set; }
    }
}
