namespace Line.Framework.Graphics;

[Flags]
public enum BufferUsage
{
    Vertex = 1 << 0,
    Index = 1 << 1,
    Indirect = 1 << 2,
    StorageRead = 1 << 3,
    StorageWrite = 1 << 4,

    /// <summary>
    /// 提示后端该缓冲会被 CPU 频繁重写。
    /// <para>
    /// 不设置也能正确工作 —— 后端会走 staging 中转（多一次内存拷贝）。
    /// 设置后 Veldrid 会使用可映射的宿主内存，省掉那次拷贝；
    /// 但在独立显卡上 GPU 读取宿主内存会略慢于显存，属于「测了才知道」的取舍。
    /// </para>
    /// <para>
    /// SDL3 无对应标志，会被忽略 —— 该后端的上传（SDL_UploadToGPUBuffer）
    /// 本身就是孤儿化的（cycle = true），不需要额外声明。
    /// </para>
    /// </summary>
    Dynamic = 1 << 5,
}