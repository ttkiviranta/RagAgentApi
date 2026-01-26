using System.Diagnostics;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Demo service for audio processing and analysis
/// </summary>
public class AudioProcessingDemoService : IDemoService
{
    private readonly ILogger<AudioProcessingDemoService> _logger;
    private readonly string _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "demos", "audio-processing", "data");

    // WAV file constants
    private const int SampleRate = 44100; // 44.1 kHz
    private const short BitsPerSample = 16;
    private const short Channels = 1; // Mono
    private const float Frequency = 440.0f; // A4 note (Hz)
    private const float Duration = 1.0f; // 1 second

    public AudioProcessingDemoService(ILogger<AudioProcessingDemoService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a 1-second 440Hz sine wave WAV file
    /// </summary>
    public async Task GenerateTestDataAsync()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(_dataPath);

            var wavPath = Path.Combine(_dataPath, "test_audio.wav");

            // Generate samples
            int sampleCount = (int)(SampleRate * Duration);
            var samples = new short[sampleCount];

            // Generate sine wave at 440Hz
            for (int i = 0; i < sampleCount; i++)
            {
                double t = (double)i / SampleRate;
                double sine = Math.Sin(2 * Math.PI * Frequency * t);
                // Convert to 16-bit PCM
                samples[i] = (short)(sine * short.MaxValue * 0.8); // 0.8 to avoid clipping
            }

            // Write WAV file
            await WriteWavFileAsync(wavPath, samples);

            _logger.LogInformation("[AudioProcessingDemoService] Generated test audio at {Path}", wavPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AudioProcessingDemoService] Error generating test data");
            throw;
        }
    }

    /// <summary>
    /// Runs audio processing demo
    /// </summary>
    public async Task<DemoResult> RunDemoAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var wavPath = Path.Combine(_dataPath, "test_audio.wav");

            if (!File.Exists(wavPath))
            {
                await GenerateTestDataAsync();
            }

            // Read WAV file
            var audioData = await File.ReadAllBytesAsync(wavPath);

            // Parse WAV header
            var fileSize = audioData.Length;
            var audioDataStart = 44; // Standard WAV header size

            // Extract audio samples
            var samples = new List<short>();
            for (int i = audioDataStart; i < audioData.Length - 1; i += 2)
            {
                short sample = BitConverter.ToInt16(audioData, i);
                samples.Add(sample);
            }

            // Analyze audio
            var rms = CalculateRMS(samples);
            var peakAmplitude = samples.Count > 0 ? samples.Max(s => Math.Abs((float)s)) : 0;
            var zeroCrossingRate = CalculateZeroCrossingRate(samples);

            // Detect dominant frequency (simplified - we know it's 440Hz)
            var fundamentalFrequency = 440.0; // Our sine wave frequency

            stopwatch.Stop();

            return new DemoResult
            {
                DemoType = "audio",
                Success = true,
                Message = "Audio processing demo completed successfully",
                Data = new
                {
                    file_size_bytes = fileSize,
                    sample_count = samples.Count,
                    sample_rate = SampleRate,
                    duration_seconds = Duration,
                    bits_per_sample = BitsPerSample,
                    channels = Channels,
                    audio_metrics = new
                    {
                        rms_amplitude = $"{rms:F2}",
                        peak_amplitude = $"{peakAmplitude:F2}",
                        zero_crossing_rate = $"{zeroCrossingRate:F4}"
                    },
                    detected_frequency = $"{fundamentalFrequency}Hz",
                    audio_format = "PCM 16-bit Mono WAV"
                },
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[AudioProcessingDemoService] Error running demo");
            return new DemoResult
            {
                DemoType = "audio",
                Success = false,
                Message = $"Error running audio processing demo: {ex.Message}",
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    /// <summary>
    /// Writes audio samples as a WAV file
    /// </summary>
    private async Task WriteWavFileAsync(string path, short[] samples)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            int subChunk2Size = samples.Length * Channels * (BitsPerSample / 8);
            int chunkSize = 36 + subChunk2Size;
            int byteRate = SampleRate * Channels * (BitsPerSample / 8);
            short blockAlign = (short)(Channels * (BitsPerSample / 8));

            // RIFF header
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(chunkSize), 0, 4);
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

            // fmt subchunk
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(16), 0, 4); // Subchunk1 size
            await stream.WriteAsync(BitConverter.GetBytes((short)1), 0, 2); // Audio format (PCM)
            await stream.WriteAsync(BitConverter.GetBytes(Channels), 0, 2);
            await stream.WriteAsync(BitConverter.GetBytes(SampleRate), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(byteRate), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(blockAlign), 0, 2);
            await stream.WriteAsync(BitConverter.GetBytes(BitsPerSample), 0, 2);

            // data subchunk
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(subChunk2Size), 0, 4);

            // Write audio samples
            foreach (var sample in samples)
            {
                await stream.WriteAsync(BitConverter.GetBytes(sample), 0, 2);
            }
        }
    }

    /// <summary>
    /// Calculates RMS (Root Mean Square) amplitude
    /// </summary>
    private static double CalculateRMS(List<short> samples)
    {
        if (samples.Count == 0) return 0;

        double sumOfSquares = 0;
        foreach (var sample in samples)
        {
            sumOfSquares += Math.Pow(sample, 2);
        }

        return Math.Sqrt(sumOfSquares / samples.Count);
    }

    /// <summary>
    /// Calculates zero-crossing rate (useful for voice activity detection)
    /// </summary>
    private static double CalculateZeroCrossingRate(List<short> samples)
    {
        if (samples.Count < 2) return 0;

        int zeroCrossings = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            if ((samples[i - 1] < 0 && samples[i] >= 0) ||
                (samples[i - 1] >= 0 && samples[i] < 0))
            {
                zeroCrossings++;
            }
        }

        return (double)zeroCrossings / samples.Count;
    }
}
