namespace Line.Framework.Audio
{
    /// <summary>
    /// 可播放音频的基本行为
    /// </summary>
    public interface IAudioPlayable
    {
        void Play();
        void Pause();
        void Stop();
        float Volume { get; set; }
    }

    /// <summary>
    /// 支持获取播放位置的音频（如 Track）
    /// </summary>
    public interface ISeekable : IAudioPlayable
    {
        double CurrentTime { get; }
        void Seek(double seconds);
    }
}