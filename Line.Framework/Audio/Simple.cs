using ManagedBass;

namespace Line.Framework.Audio
{
    public class Sample
    {
        private readonly int sampleHandle;

        public Sample(string filePath)
        {
            sampleHandle = Bass.SampleLoad(filePath, 0, 0, 1, BassFlags.Default);
            if (sampleHandle == 0)
                throw new Exception($"Failed to load sample: {filePath} (Error: {Bass.LastError})");
        }

        public void Play()
        {
            int channel = Bass.SampleGetChannel(sampleHandle);
            Bass.ChannelPlay(channel);
        }

        public void Play(float volume, float pan = 0f)
        {
            int channel = Bass.SampleGetChannel(sampleHandle);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);
            Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, pan);
            Bass.ChannelPlay(channel);
        }
    }
}