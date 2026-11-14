namespace Line.Framework.Graphics
{
    public struct SwapchainSource
    {
        public nint Surface { get; set; }
        public nint Connection { get; set; }
        public NativeWindowKind Kind { get; set; }
    }

    public enum NativeWindowKind
    {
        Win32,
        Xlib,
        Wayland,
        Cocoa, // macOS 的 NSWindow
        AndroidSurface,
        MetalView, // NSView
    }
}
