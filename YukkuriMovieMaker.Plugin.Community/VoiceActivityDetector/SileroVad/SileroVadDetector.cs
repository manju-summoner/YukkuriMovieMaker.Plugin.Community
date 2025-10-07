// Based on: https://github.com/snakers4/silero-vad
// Original work Copyright (c) 2020-present Silero Team
// Modified by manju summoner
// Licensed under the MIT License


// Based on: https://github.com/snakers4/silero-vad
// Original work Copyright (c) 2020-present Silero Team
// Modified by manju summoner
// Licensed under the MIT License

namespace YukkuriMovieMaker.Plugin.Community.VoiceActivityDetector.SileroVad
{
    public class SileroVadDetector : IDisposable
    {
        private readonly SileroVadOnnxModel _model;
        private readonly float _threshold;
        private readonly float _negThreshold;
        private readonly int _samplingRate;
        private readonly int _windowSizeSample;
        private readonly float _minSpeechSamples;
        private readonly float _speechPadSamples;
        private readonly float _maxSpeechSamples;
        private readonly float _minSilenceSamples;
        private readonly float _minSilenceSamplesAtMaxSpeech;
        private int _audioLengthSamples;
        private const float THRESHOLD_GAP = 0.15f;
        private const int SAMPLING_RATE_8K = 8000;
        private const int SAMPLING_RATE_16K = 16000;

        public SileroVadDetector(string onnxModelPath, float threshold = 0.5f, int samplingRate = SAMPLING_RATE_16K,
            int minSpeechDurationMs = 250, float maxSpeechDurationSeconds = float.PositiveInfinity,
            int minSilenceDurationMs = 100, int speechPadMs = 30)
        {
            if (samplingRate != SAMPLING_RATE_8K && samplingRate != SAMPLING_RATE_16K)
            {
                throw new ArgumentException("Sampling rate not support, only available for [8000, 16000]");
            }

            _model = new SileroVadOnnxModel(onnxModelPath);
            _samplingRate = samplingRate;
            _threshold = threshold;
            _negThreshold = threshold - THRESHOLD_GAP;
            _windowSizeSample = samplingRate == SAMPLING_RATE_16K ? 512 : 256;
            _minSpeechSamples = samplingRate * minSpeechDurationMs / 1000f;
            _speechPadSamples = samplingRate * speechPadMs / 1000f;
            _maxSpeechSamples = samplingRate * maxSpeechDurationSeconds - _windowSizeSample - 2 * _speechPadSamples;
            _minSilenceSamples = samplingRate * minSilenceDurationMs / 1000f;
            _minSilenceSamplesAtMaxSpeech = samplingRate * 98 / 1000f;
            Reset();
        }

        public void Reset()
        {
            _model.ResetStates();
        }

        public List<SileroSpeechSegment> GetSpeechSegmentList(ReadOnlySpan<float> input)
        {
            Reset();
            var speechProbList = new List<float>();
            _audioLengthSamples = input.Length;
            float[] buffer = new float[_windowSizeSample];

            for (int i = 0; i < input.Length; i += _windowSizeSample)
            {
                input[i..Math.Min(i + _windowSizeSample, input.Length)].CopyTo(buffer);
                float speechProb = _model.Call([buffer], _samplingRate)[0];
                speechProbList.Add(speechProb);
            }
            return CalculateProb(speechProbList);

        }

        private List<SileroSpeechSegment> CalculateProb(List<float> speechProbList)
        {
            var result = new List<SileroSpeechSegment>();
            bool triggered = false;
            int tempEnd = 0, prevEnd = 0, nextStart = 0;
            var segment = new SileroSpeechSegment();
            bool isDetecting = false;

            for (int i = 0; i < speechProbList.Count; i++)
            {
                float speechProb = speechProbList[i];
                if (speechProb >= _threshold && tempEnd != 0)
                {
                    tempEnd = 0;
                    if (nextStart < prevEnd)
                    {
                        nextStart = _windowSizeSample * i;
                    }
                }

                if (speechProb >= _threshold && !triggered)
                {
                    triggered = true;
                    isDetecting = true;
                    segment.StartOffset = _windowSizeSample * i;
                    continue;
                }

                if (triggered && _windowSizeSample * i - segment.StartOffset > _maxSpeechSamples)
                {
                    if (prevEnd != 0)
                    {
                        segment.EndOffset = prevEnd;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        if (nextStart < prevEnd)
                        {
                            triggered = false;
                            isDetecting = false;
                        }
                        else
                        {
                            segment.StartOffset = nextStart;
                            isDetecting = true;
                        }

                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                    }
                    else
                    {
                        segment.EndOffset = _windowSizeSample * i;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                        triggered = false;
                        isDetecting = false;
                        continue;
                    }
                }

                if (speechProb < _negThreshold && triggered)
                {
                    if (tempEnd == 0)
                    {
                        tempEnd = _windowSizeSample * i;
                    }

                    if (_windowSizeSample * i - tempEnd > _minSilenceSamplesAtMaxSpeech)
                    {
                        prevEnd = tempEnd;
                    }

                    if (_windowSizeSample * i - tempEnd < _minSilenceSamples)
                    {
                        continue;
                    }
                    else
                    {
                        segment.EndOffset = tempEnd;
                        if (segment.EndOffset - segment.StartOffset > _minSpeechSamples)
                        {
                            result.Add(segment);
                        }

                        segment = new SileroSpeechSegment();
                        prevEnd = 0;
                        nextStart = 0;
                        tempEnd = 0;
                        triggered = false;
                        isDetecting = false;
                        continue;
                    }
                }
            }

            if (isDetecting && _audioLengthSamples - segment.StartOffset > _minSpeechSamples)
            {
                segment.EndOffset = _audioLengthSamples;
                result.Add(segment);
            }

            for (int i = 0; i < result.Count; i++)
            {
                var item = result[i];
                if (i == 0)
                {
                    item.StartOffset = (int)Math.Max(0, item.StartOffset - _speechPadSamples);
                }

                if (i != result.Count - 1)
                {
                    var nextItem = result[i + 1];
                    int silenceDuration = nextItem.StartOffset - item.EndOffset;
                    if (silenceDuration < 2 * _speechPadSamples)
                    {
                        item.EndOffset += silenceDuration / 2;
                        nextItem.StartOffset = Math.Max(0, nextItem.StartOffset - silenceDuration / 2);
                    }
                    else
                    {
                        item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset + _speechPadSamples);
                        nextItem.StartOffset = (int)Math.Max(0, nextItem.StartOffset - _speechPadSamples);
                    }
                }
                else
                {
                    item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset + _speechPadSamples);
                }
            }

            return MergeListAndCalculateSecond(result, _samplingRate);
        }

        private static List<SileroSpeechSegment> MergeListAndCalculateSecond(List<SileroSpeechSegment> original, int samplingRate)
        {
            var result = new List<SileroSpeechSegment>();
            if (original == null || original.Count == 0)
            {
                return result;
            }

            int left = original[0].StartOffset;
            int right = original[0].EndOffset;
            if (original.Count > 1)
            {
                original.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
                for (int i = 1; i < original.Count; i++)
                {
                    SileroSpeechSegment segment = original[i];

                    if (segment.StartOffset > right)
                    {
                        result.Add(new SileroSpeechSegment(left, right,
                            CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
                        left = segment.StartOffset;
                        right = segment.EndOffset;
                    }
                    else
                    {
                        right = Math.Max(right, segment.EndOffset);
                    }
                }

                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
            }
            else
            {
                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate), CalculateSecondByOffset(right, samplingRate)));
            }

            return result;
        }

        private static float CalculateSecondByOffset(int offset, int samplingRate)
        {
            float secondValue = offset * 1.0f / samplingRate;
            return (float)Math.Floor(secondValue * 1000.0f) / 1000.0f;
        }

        public void Dispose()
        {
            _model?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
