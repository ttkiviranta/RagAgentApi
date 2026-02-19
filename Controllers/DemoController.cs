using Microsoft.AspNetCore.Mvc;
using RagAgentApi.Services.DemoServices;

namespace RagAgentApi.Controllers;

/// <summary>
/// Controller for managing and running AI/ML demonstration services
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DemoController> _logger;

    private static readonly string[] AvailableDemos = { "classification", "time-series", "image", "audio", "a2a-pipeline" };

    public DemoController(IServiceProvider serviceProvider, ILogger<DemoController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get list of available demo types
    /// </summary>
    /// <returns>Array of available demo names</returns>
    /// <response code="200">Returns list of available demos</response>
    [HttpGet("available")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetAvailableDemos()
    {
        _logger.LogInformation("[DemoController] GetAvailableDemos called");
        return Ok(AvailableDemos);
    }

    /// <summary>
    /// Generate test data for a specific demo type
    /// </summary>
    /// <param name="demoType">Type of demo: classification, time-series, image, or audio</param>
    /// <returns>Status of test data generation</returns>
    /// <response code="200">Test data generated successfully</response>
    /// <response code="400">Invalid demo type</response>
    /// <response code="500">Error generating test data</response>
    [HttpPost("generate-testdata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateTestData([FromQuery] string demoType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(demoType))
            {
                return BadRequest(new { error = "demoType parameter is required" });
            }

            demoType = demoType.ToLower().Trim();

            if (!AvailableDemos.Contains(demoType))
            {
                return BadRequest(new
                {
                    error = $"Invalid demo type '{demoType}'",
                    available_demos = AvailableDemos
                });
            }

            _logger.LogInformation("[DemoController] Generating test data for demo type: {DemoType}", demoType);

            var service = GetDemoService(demoType);
            if (service == null)
            {
                return StatusCode(500, new { error = $"Demo service for type '{demoType}' not found" });
            }

            await service.GenerateTestDataAsync();

            return Ok(new
            {
                success = true,
                message = $"Test data generated successfully for {demoType} demo",
                demo_type = demoType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DemoController] Error generating test data for demo type: {DemoType}", demoType);
            return StatusCode(500, new
            {
                error = "Error generating test data",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Run a specific demo and return results
    /// </summary>
    /// <param name="demoType">Type of demo: classification, time-series, image, or audio</param>
    /// <returns>Demo results including metrics and analysis</returns>
    /// <response code="200">Demo executed successfully</response>
    /// <response code="400">Invalid demo type</response>
    /// <response code="500">Error running demo</response>
    [HttpPost("run")]
    [ProducesResponseType(typeof(DemoResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RunDemo([FromQuery] string demoType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(demoType))
            {
                return BadRequest(new { error = "demoType parameter is required" });
            }

            demoType = demoType.ToLower().Trim();

            if (!AvailableDemos.Contains(demoType))
            {
                return BadRequest(new
                {
                    error = $"Invalid demo type '{demoType}'",
                    available_demos = AvailableDemos
                });
            }

            _logger.LogInformation("[DemoController] Running demo: {DemoType}", demoType);

            var service = GetDemoService(demoType);
            if (service == null)
            {
                return StatusCode(500, new { error = $"Demo service for type '{demoType}' not found" });
            }

            var result = await service.RunDemoAsync();

            if (!result.Success)
            {
                _logger.LogWarning("[DemoController] Demo {DemoType} failed: {Message}", demoType, result.Message);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DemoController] Error running demo: {DemoType}", demoType);
            return StatusCode(500, new
            {
                error = "Error running demo",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Gets the appropriate demo service based on type
    /// </summary>
    private IDemoService? GetDemoService(string demoType)
    {
        return demoType switch
        {
            "classification" => _serviceProvider.GetService<ClassificationDemoService>(),
            "time-series" => _serviceProvider.GetService<TimeSeriesDemoService>(),
            "image" => _serviceProvider.GetService<ImageProcessingDemoService>(),
            "audio" => _serviceProvider.GetService<AudioProcessingDemoService>(),
            "a2a-pipeline" => _serviceProvider.GetService<A2ADemoService>(),
            _ => null
        };
    }
}
