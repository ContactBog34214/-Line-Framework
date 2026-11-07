using Line.Framework.Types;

namespace Line.Framework.Graphics
{
    public interface ICommandList : IDisposable, IName
    {
        bool Working { get; set; }
        IDisposable Record();
        void SetFrameBuffer(IFrameBuffer frameBuffer);
        void ClearColorTarget(uint index, RgbaFloat color);
        void SetPipeline(IPipeline pipeline);
        void SetVertexBuffer(uint slot, IDeviceBuffer buffer);
        void SetGraphicsResourceSet(uint slot, IResourceSet resourceSet);
        void Draw(uint vertexCount, uint instanceCount, uint vertexStart, uint instanceStart);
        void SetScissorRect(uint index, uint x, uint y, uint width, uint height);
    }
}
