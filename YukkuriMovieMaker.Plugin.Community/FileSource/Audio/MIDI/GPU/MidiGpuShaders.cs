using ComputeSharp;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.GPU;

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LimiterShader(ReadWriteBuffer<float> buffer, float threshold) : IComputeShader
{
    private readonly ReadWriteBuffer<float> buffer = buffer;
    private readonly float threshold = threshold;

    public void Execute()
    {
        int i = ThreadIds.X;
        buffer[i] = Hlsl.Clamp(buffer[i], -threshold, threshold);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CompressionShader(ReadWriteBuffer<float> buffer, float threshold, float ratio) : IComputeShader
{
    private readonly ReadWriteBuffer<float> buffer = buffer;
    private readonly float threshold = threshold;
    private readonly float ratio = ratio;

    public void Execute()
    {
        int i = ThreadIds.X;
        float sample = buffer[i];
        float absSample = Hlsl.Abs(sample);
        if (absSample > threshold)
        {
            float excess = absSample - threshold;
            float compressed = threshold + excess / ratio;
            buffer[i] = (sample / absSample) * compressed;
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ReverbShader(ReadOnlyBuffer<float> input, ReadWriteBuffer<float> output, int delaySamples, float decay, int bufferLength) : IComputeShader
{
    private readonly ReadOnlyBuffer<float> input = input;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int delaySamples = delaySamples;
    private readonly float decay = decay;
    private readonly int bufferLength = bufferLength;

    public void Execute()
    {
        int i = ThreadIds.X;
        if (i >= bufferLength) return;
        if (i >= delaySamples)
            output[i] = input[i] + input[i - delaySamples] * decay;
        else
            output[i] = input[i];
    }
}
