using Line.Framework.Graphics;
using SDL3;

namespace Line.Framework.Default.Graphics.SDL3
{
    public class SDLBuffer : IDeviceBuffer
    {
        private readonly IntPtr dev;
        internal IntPtr handle;
        public ulong SizeInBytes => throw new NotImplementedException();

        public BufferUsage Usage => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public void Dispose()
        {
            SDL.ReleaseGPUBuffer(dev, handle);
            handle = IntPtr.Zero;
        }

        public void Update(ulong offset, ReadOnlySpan<byte> source)
        {
            throw new NotImplementedException();
        }
    }
}
