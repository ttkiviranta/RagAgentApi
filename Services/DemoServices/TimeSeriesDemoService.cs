using System.Diagnostics;

namespace RagAgentApi.Services.DemoServices;

/// <summary>
/// Demo service for time-series forecasting using ML.NET
/// </summary>
public class TimeSeriesDemoService : IDemoService
{
    private readonly ILogger<TimeSeriesDemoService> _logger;
    private readonly string _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "demos", "time-series", "data");

    public TimeSeriesDemoService(ILogger<TimeSeriesDemoService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates sample time-series data with trend and random variation
    /// </summary>
    public async Task GenerateTestDataAsync()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(_dataPath);

            var csvPath = Path.Combine(_dataPath, "timeseries_data.csv");
            var random = new Random(42); // Fixed seed for reproducibility

            // Generate 100 days of data with trend
            var data = new List<(DateTime Date, double Value)>();
            var baseDate = DateTime.Now.AddDays(-100);

            for (int i = 0; i < 100; i++)
            {
                var date = baseDate.AddDays(i);
                // Linear trend + random noise
                var trend = 50 + (i * 0.5); // Increasing trend
                var noise = (random.NextDouble() - 0.5) * 10; // Random variation
                var value = trend + noise;

                data.Add((date, Math.Round(value, 2)));
            }

            // Write CSV file
            using (var writer = new StreamWriter(csvPath))
            {
                await writer.WriteLineAsync("date,value");
                foreach (var (date, value) in data)
                {
                    await writer.WriteLineAsync($"{date:yyyy-MM-dd},{value}");
                }
            }

            _logger.LogInformation("[TimeSeriesDemoService] Generated test data at {Path}", csvPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TimeSeriesDemoService] Error generating test data");
            throw;
        }
    }

    /// <summary>
    /// Runs time-series forecasting demo
    /// </summary>
    public async Task<DemoResult> RunDemoAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var csvPath = Path.Combine(_dataPath, "timeseries_data.csv");

            if (!File.Exists(csvPath))
            {
                await GenerateTestDataAsync();
            }

            // Read data
            var lines = await File.ReadAllLinesAsync(csvPath);
            var values = new List<double>();

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && double.TryParse(parts[1], out var value))
                {
                    values.Add(value);
                }
            }

            // Calculate statistics
            var average = values.Count > 0 ? values.Average() : 0;
            var min = values.Count > 0 ? values.Min() : 0;
            var max = values.Count > 0 ? values.Max() : 0;
            var stdDev = CalculateStandardDeviation(values);

            // Simple trend analysis - compare first and last 10 days
            var firstPeriodAvg = values.Take(10).Average();
            var lastPeriodAvg = values.TakeLast(10).Average();
            var trend = lastPeriodAvg > firstPeriodAvg ? "upward" : "downward";

            // Forecast next 5 days
            var forecast = ForecastNextDays(values, 5);

            stopwatch.Stop();

            return new DemoResult
            {
                DemoType = "time-series",
                Success = true,
                Message = "Time-series demo completed successfully",
                Data = new
                {
                    data_points = values.Count,
                    statistics = new
                    {
                        average = $"{average:F2}",
                        minimum = $"{min:F2}",
                        maximum = $"{max:F2}",
                        std_deviation = $"{stdDev:F2}"
                    },
                    trend = trend,
                    forecast_5_days = forecast.Select(f => $"{f:F2}").ToList()
                },
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[TimeSeriesDemoService] Error running demo");
            return new DemoResult
            {
                DemoType = "time-series",
                Success = false,
                Message = $"Error running time-series demo: {ex.Message}",
                ExecutionTimeMs = $"{stopwatch.ElapsedMilliseconds}ms"
            };
        }
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count == 0) return 0;

        var average = values.Average();
        var sumOfSquares = values.Sum(x => Math.Pow(x - average, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    private static List<double> ForecastNextDays(List<double> values, int days)
    {
        var forecast = new List<double>();
        
        if (values.Count < 2)
            return forecast;

        // Simple linear extrapolation
        var lastValue = values[^1];
        var trend = (values[^1] - values[^2]) / 1.0; // Daily change

        for (int i = 1; i <= days; i++)
        {
            forecast.Add(lastValue + (trend * i));
        }

        return forecast;
    }
}
