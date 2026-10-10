namespace Line.Framework.Graphics
{
    public struct BlendAttachmentDescription
    {
        public bool BlendEnabled { get; set; }
        public BlendFactor SourceColorFactor { get; set; }
        public BlendFactor DestinationColorFactor { get; set; }
        public ColorWriteMask? ColorWriteMask { get; set; }
        public BlendFactor SourceAlphaFactor { get; set; }
        public BlendFactor DestinationAlphaFactor { get; set; }
        public BlendOperation ColorFunction { get; set; }
        public BlendOperation AlphaFunction { get; set; }

        public static readonly BlendAttachmentDescription OverrideBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.One,
                DestinationColorFactor = BlendFactor.Zero,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.One,
                DestinationAlphaFactor = BlendFactor.Zero,
                AlphaFunction = BlendOperation.Add,
            };
        public static readonly BlendAttachmentDescription AlphaBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.SourceAlpha,
                DestinationColorFactor = BlendFactor.InverseSourceAlpha,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.SourceAlpha,
                DestinationAlphaFactor = BlendFactor.InverseSourceAlpha,
                AlphaFunction = BlendOperation.Add,
            };
        public static readonly BlendAttachmentDescription AdditiveBlend =
            new BlendAttachmentDescription
            {
                BlendEnabled = true,
                SourceColorFactor = BlendFactor.SourceAlpha,
                DestinationColorFactor = BlendFactor.One,
                ColorFunction = BlendOperation.Add,
                SourceAlphaFactor = BlendFactor.SourceAlpha,
                DestinationAlphaFactor = BlendFactor.One,
                AlphaFunction = BlendOperation.Add,
            };
        public static readonly BlendAttachmentDescription Disabled = new BlendAttachmentDescription
        {
            BlendEnabled = false,
            SourceColorFactor = BlendFactor.One,
            DestinationColorFactor = BlendFactor.Zero,
            ColorFunction = BlendOperation.Add,
            SourceAlphaFactor = BlendFactor.One,
            DestinationAlphaFactor = BlendFactor.Zero,
            AlphaFunction = BlendOperation.Add,
        };
    }
}
