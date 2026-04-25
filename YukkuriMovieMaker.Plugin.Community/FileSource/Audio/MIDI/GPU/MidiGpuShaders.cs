using ComputeSharp;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.GPU;

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct NormalizeSamplesShader : IComputeShader
{
    private readonly ReadWriteBuffer<float> buffer;
    private readonly float scale;

    public NormalizeSamplesShader(ReadWriteBuffer<float> buffer, float scale)
    {
        this.buffer = buffer;
        this.scale = scale;
    }

    public void Execute()
    {
        int i = ThreadIds.X;
        buffer[i] = Hlsl.Clamp(buffer[i] * scale, -1.0f, 1.0f);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LimiterShader : IComputeShader
{
    private readonly ReadWriteBuffer<float> buffer;
    private readonly float threshold;

    public LimiterShader(ReadWriteBuffer<float> buffer, float threshold)
    {
        this.buffer = buffer;
        this.threshold = threshold;
    }

    public void Execute()
    {
        int i = ThreadIds.X;
        buffer[i] = Hlsl.Clamp(buffer[i], -threshold, threshold);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CompressionShader : IComputeShader
{
    private readonly ReadWriteBuffer<float> buffer;
    private readonly float threshold;
    private readonly float ratio;

    public CompressionShader(ReadWriteBuffer<float> buffer, float threshold, float ratio)
    {
        this.buffer = buffer;
        this.threshold = threshold;
        this.ratio = ratio;
    }

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
internal readonly partial struct ReverbShader : IComputeShader
{
    private readonly ReadOnlyBuffer<float> input;
    private readonly ReadWriteBuffer<float> output;
    private readonly int delaySamples;
    private readonly float decay;
    private readonly int bufferLength;

    public ReverbShader(ReadOnlyBuffer<float> input, ReadWriteBuffer<float> output, int delaySamples, float decay, int bufferLength)
    {
        this.input = input;
        this.output = output;
        this.delaySamples = delaySamples;
        this.decay = decay;
        this.bufferLength = bufferLength;
    }

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
