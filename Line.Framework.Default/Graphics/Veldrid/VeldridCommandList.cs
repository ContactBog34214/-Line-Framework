using Line.Framework.Graphics;
using Line.Framework.Types;

namespace Line.Framework.Default.Graphics.Veldrid
{
    public class VeldridCommandList : ICommandList
    {
        internal VeldridCommandList(global::Veldrid.CommandList commandList)
        {
            cl = commandList;
        }

        internal global::Veldrid.CommandList cl { get; }
        private readonly Lock workingStateLock = new();

        public bool Working
        {
            get;
            set
            {
                lock (workingStateLock)
                {
                    if (value == field)
                        if (value)
                            throw new InvalidOperationException(
                                $"Command state already is {field}"
                            );
                        else
                            return;
                    if (value)
                        cl.Begin();
                    else
                        cl.End();
                    field = value;
                }
            }
        } = false;

        public string Name => cl?.Name;

        public void ClearColorTarget(uint index, RgbaFloat color)
        {
            cl.ClearColorTarget(index, VeldridConverter.ConvertRgbaFloat(color));
        }

        public void Dispose()
        {
            if (Working)
                Working = false;
            cl?.Dispose();
        }

        public void Draw(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart)
        {
            cl.Draw(vertexCount, instanceCount, vertexStart, instanceStart);
        }

        public IDisposable Record()
        {
            if (Working)
                throw new InvalidOperationException("Already recording");
            Working = true;
            return new _RecordScope(this);
        }

        private class _RecordScope(VeldridCommandList t) : IDisposable
        {
            private bool released = false;

            public void Dispose()
            {
                if (released)
                    return;
                released = true;
                t?.Working = false;
            }
        }

        public void SetFrameBuffer(IFrameBuffer frameBuffer)
        {
            if (!(frameBuffer is VeldridFrameBuffer fb))
                throw new InvalidDataException();
            cl.SetFramebuffer(fb.framebuffer);
        }

        public void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet)
        {
            if (!(resourceSet is VeldridResourceSet rs))
                throw new InvalidDataException();
            cl.SetGraphicsResourceSet(slot, rs.ResourceSet);
        }

        public void SetPipeline(IPipeline pipeline)
        {
            if (!(pipeline is VeldridPipeline pl))
                throw new InvalidDataException();
            cl.SetPipeline(pl.Pipeline);
        }

        public void SetScissorRect(uint index, uint x, uint y, uint width, uint height)
        {
            cl.SetScissorRect(index, x, y, width, height);
        }

        public void SetVertexBuffer(uint slot, IDeviceBuffer buffer)
        {
            if (!(buffer is VeldridBuffer bf))
                throw new InvalidDataException();
            cl.SetVertexBuffer(slot, bf.buffer);
        }
    }

    public partial class VeldridResourceFactory
    {
        public ICommandList CreateCommandList(string Name = null)
        {
            var cl = dev.ResourceFactory.CreateCommandList();
            cl.Name = Name;
            return new VeldridCommandList(cl);
        }
    }
}
