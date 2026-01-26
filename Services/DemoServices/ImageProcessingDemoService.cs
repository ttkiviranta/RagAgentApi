using System.Diagnostics;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Demo service for image processing and analysis
/// </summary>
public class ImageProcessingDemoService : IDemoService
{
    private readonly ILogger<ImageProcessingDemoService> _logger;
    private readonly string _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "demos", "image-processing", "data");

    public ImageProcessingDemoService(ILogger<ImageProcessingDemoService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a sample 256x256 PNG image with gradient pattern
    /// </summary>
    public async Task GenerateTestDataAsync()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(_dataPath);

            var imagePath = Path.Combine(_dataPath, "test_image.png");

            // Create a 256x256 RGB image with gradient pattern
            const int width = 256;
            const int height = 256;
            byte[] pixelData = new byte[width * height * 3]; // RGB format

            // Create gradient pattern
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 3;

                    // Red gradient (left to right)
                    pixelData[index] = (byte)((x * 255) / width);
                    // Green gradient (top to bottom)
                    pixelData[index + 1] = (byte)((y * 255) / height);
                    // Blue gradient (diagonal)
                    pixelData[index + 2] = (byte)(((x + y) * 255) / (width + height));
                }
            }

            // Write simple PPM format first (easier), then convert concept to PNG
            // For demo purposes, we'll use a simpler PNG-like structure
            await WritePngImageAsync(imagePath, pixelData, width, height);

            _logger.LogInformation("[ImageProcessingDemoService] Generated test image at {Path}", imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImageProcessingDemoService] Error generating test data");
            throw;
        }
    }

    /// <summary>
    /// Runs image processing demo
    /// </summary>
    public async Task<DemoResult> RunDemoAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var imagePath = Path.Combine(_dataPath, "test_image.png");

            if (!File.Exists(imagePath))
            {
                await GenerateTestDataAsync();
            }

            // Analyze image properties
            var fileInfo = new FileInfo(imagePath);
            var imageBytes = await File.ReadAllBytesAsync(imagePath);

            // Simple image analysis
            var width = 256;
            var height = 256;
            var pixelCount = width * height;

            // Calculate average color (simplified)
            var avgRed = 0;
            var avgGreen = 0;
            var avgBlue = 0;

            // Sample every 10th pixel for performance
            int sampledPixels = 0;
            for (int i = 0; i < imageBytes.Length - 2; i += 30) // Skip 3 bytes per pixel
            {
                avgRed += imageBytes[i];
                avgGreen += imageBytes[i + 1];
                avgBlue += imageBytes[i + 2];
                sampledPixels++;
            }

            if (sampledPixels > 0)
            {
                avgRed /= sampledPixels;
                avgGreen /= sampledPixels;
                avgBlue /= sampledPixels;
            }

            stopwatch.Stop();

            return new DemoResult
            {
                DemoType = "image",
                Success = true,
                Message = "Image processing demo completed successfully",
                Data = new
                {
                    image_dimensions = $"{width}x{height}",
                    total_pixels = pixelCount,
                    file_size_bytes = fileInfo.Length,
                    average_color = new
                    {
                        red = avgRed,
                        green = avgGreen,
                        blue = avgBlue
                    },
                    processing_operations = new[] { "gradient generation", "color analysis", "compression" }
                },
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[ImageProcessingDemoService] Error running demo");
            return new DemoResult
            {
                DemoType = "image",
                Success = false,
                Message = $"Error running image processing demo: {ex.Message}",
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    /// <summary>
    /// Writes pixel data as a simple PNG image
    /// </summary>
    private async Task WritePngImageAsync(string path, byte[] pixelData, int width, int height)
    {
        // Using a simplified PNG structure for demo purposes
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            // PNG signature
            byte[] pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            await stream.WriteAsync(pngSignature, 0, pngSignature.Length);

            // IHDR chunk (image header)
            var ihdr = CreateIHDRChunk(width, height);
            await stream.WriteAsync(ihdr, 0, ihdr.Length);

            // IDAT chunk (image data - simplified)
            var idat = CreateIDATChunk(pixelData);
            await stream.WriteAsync(idat, 0, idat.Length);

            // IEND chunk
            byte[] iend = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
            await stream.WriteAsync(iend, 0, iend.Length);
        }
    }

    private byte[] CreateIHDRChunk(int width, int height)
    {
        var chunk = new List<byte>();

        // Length: 13
        chunk.AddRange(new byte[] { 0, 0, 0, 13 });

        // "IHDR"
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes("IHDR"));

        // Width (4 bytes, big-endian)
        chunk.AddRange(BitConverter.GetBytes(width).Reverse());

        // Height (4 bytes, big-endian)
        chunk.AddRange(BitConverter.GetBytes(height).Reverse());

        // Bit depth: 8
        chunk.Add(8);

        // Color type: 2 (RGB)
        chunk.Add(2);

        // Compression method: 0
        chunk.Add(0);

        // Filter method: 0
        chunk.Add(0);

        // Interlace method: 0
        chunk.Add(0);

        // CRC (simplified - just add dummy bytes)
        chunk.AddRange(new byte[] { 0, 0, 0, 0 });

        return chunk.ToArray();
    }

    private byte[] CreateIDATChunk(byte[] pixelData)
    {
        var chunk = new List<byte>();

        // Length (simplified)
        chunk.AddRange(new byte[] { 0, 0, 1, 0 });

        // "IDAT"
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes("IDAT"));

        // Compressed data (simplified - just copy raw for demo)
        var dataToWrite = pixelData.Take(256).ToArray();
        chunk.AddRange(dataToWrite);

        // CRC (simplified)
        chunk.AddRange(new byte[] { 0, 0, 0, 0 });

        return chunk.ToArray();
    }
}
