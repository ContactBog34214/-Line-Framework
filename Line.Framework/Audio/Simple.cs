using ManagedBass;

namespace Line.Framework.Audio
{
    public class Sample
    {
        private readonly int sampleHandle;

        public Sample(AudioThread audioThread, string filePath)
        {
            // 由于 Bass.SampleLoad 必须在音频线程调用，这里使用 audioThread.PostSync
            sampleHandle = audioThread.PostSync(() =>
                Bass.SampleLoad(filePath, 0, 0, 1, BassFlags.Default)
            );
            if (sampleHandle == 0)
                throw new Exception($"Failed to load sample: {filePath}");
        }

        public void Play()
        {
            // 播放音效：获取通道并播放
            int channel = Bass.SampleGetChannel(sampleHandle);
            Bass.ChannelPlay(channel);
        }

        public void Play(float volume, float pan = 0f)
        {
            var channel = Bass.SampleGetChannel(sampleHandle);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, pan);
            Bass.ChannelPlay(channel);
        }
    }
}
