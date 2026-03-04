using System.Text;
using System.Text.Json;
using AIMonitoringAgent.Shared.Models;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace AIMonitoringAgent.Shared.Services;

public interface IDeploymentCorrelator
{
    Task<DeploymentCorrelation?> CorrelateDeploymentAsync(
        DateTime errorTime,
        string? operationName = null);
}

public class DeploymentCorrelator : IDeploymentCorrelator
{
    private readonly RestClient _client;
    private readonly ILogger<DeploymentCorrelator> _logger;
    private readonly string _organization;
    private readonly string _project;
    private readonly string _patToken;

    public DeploymentCorrelator(
        string organizationUrl,
        string project,
        string patToken,
        ILogger<DeploymentCorrelator> logger)
    {
        _organization = organizationUrl;
        _project = project;
        _patToken = patToken;
        _logger = logger;
        _client = new RestClient(organizationUrl);
    }

    public async Task<DeploymentCorrelation?> CorrelateDeploymentAsync(
        DateTime errorTime,
        string? operationName = null)
    {
        try
        {
            var recentDeployments = await GetRecentDeploymentsAsync(errorTime);
            if (recentDeployments.Count == 0)
            {
                _logger.LogWarning("No recent deployments found for error time {ErrorTime}", errorTime);
                return null;
            }

            var mostRecentDeployment = recentDeployments.First();
            var timeToError = (errorTime - mostRecentDeployment.DeploymentTime).TotalMinutes;

            // Consider it a likely cause if error occurred within 30 minutes of deployment
            var isLikelyCause = timeToError >= 0 && timeToError <= 30;

            var correlation = new DeploymentCorrelation
            {
                DeploymentId = mostRecentDeployment.DeploymentId,
                DeploymentTime = mostRecentDeployment.DeploymentTime,
                ReleaseName = mostRecentDeployment.ReleaseName,
                CommitHash = mostRecentDeployment.CommitHash,
                Author = mostRecentDeployment.Author,
                ChangedFiles = mostRecentDeployment.ChangedFiles,
                TimeToErrorMinutes = timeToError,
                IsLikeCause = isLikelyCause
            };

            _logger.LogInformation(
                "Correlated deployment {ReleaseName} with error (time delta: {TimeToError} minutes)",
                correlation.ReleaseName,
                timeToError);

            return correlation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to correlate deployment");
            return null;
        }
    }

    private async Task<List<DeploymentInfo>> GetRecentDeploymentsAsync(DateTime errorTime)
    {
        try
        {
            var deployments = new List<DeploymentInfo>();

            // Get recent releases
            var request = new RestRequest(
                $"{_organization}/{_project}/_apis/release/releases?api-version=7.0",
                Method.Get);

            request.AddHeader("Authorization", GetAuthHeader());

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                _logger.LogWarning("Failed to fetch releases: {StatusCode}", response.StatusCode);
                return deployments;
            }

            using var document = JsonDocument.Parse(response.Content ?? "{}");
            var root = document.RootElement;

            if (!root.TryGetProperty("value", out var values))
            {
                return deployments;
            }

            foreach (var release in values.EnumerateArray().Take(10))
            {
                if (!release.TryGetProperty("createdOn", out var createdOnProperty) ||
                    !DateTime.TryParse(createdOnProperty.GetString(), out var releaseTime))
                {
                    continue;
                }

                // Only include releases from the last 24 hours before the error
                if (releaseTime > errorTime || (errorTime - releaseTime).TotalHours > 24)
                {
                    continue;
                }

                var deployment = new DeploymentInfo
                {
                    DeploymentId = GetString(release, "id"),
                    ReleaseName = GetString(release, "name"),
                    DeploymentTime = releaseTime,
                    CommitHash = GetString(release, "artifacts[0].definitionReference.sourceVersion.id"),
                    Author = GetString(release, "createdBy.displayName")
                };

                // Get changed files from commit
                deployment.ChangedFiles = await GetChangedFilesAsync(deployment.CommitHash);

                deployments.Add(deployment);
            }

            return deployments.OrderByDescending(d => d.DeploymentTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent deployments");
            return new List<DeploymentInfo>();
        }
    }

    private async Task<List<string>> GetChangedFilesAsync(string commitHash)
    {
        try
        {
            if (string.IsNullOrEmpty(commitHash))
                return new List<string>();

            var request = new RestRequest(
                $"{_organization}/{_project}/_apis/git/repositories/commits/{commitHash}/changes?api-version=7.0",
                Method.Get);

            request.AddHeader("Authorization", GetAuthHeader());

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
                return new List<string>();

            using var document = JsonDocument.Parse(response.Content ?? "{}");
            var root = document.RootElement;

            var files = new List<string>();
            if (root.TryGetProperty("changes", out var changes))
            {
                foreach (var change in changes.EnumerateArray().Take(20))
                {
                    var item = GetString(change, "item.path");
                    if (!string.IsNullOrEmpty(item))
                    {
                        files.Add(item);
                    }
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get changed files for commit {CommitHash}", commitHash);
            return new List<string>();
        }
    }

    private string GetAuthHeader()
    {
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_patToken}"));
        return $"Basic {auth}";
    }

    private static string GetString(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (part.Contains("[") && part.Contains("]"))
            {
                var propertyName = part.Substring(0, part.IndexOf('['));
                var index = int.Parse(part.Substring(part.IndexOf('[') + 1, part.IndexOf(']') - part.IndexOf('[') - 1));

                if (!current.TryGetProperty(propertyName, out var array) ||
                    array.ValueKind != JsonValueKind.Array)
                {
                    return string.Empty;
                }

                var items = array.EnumerateArray().ToList();
                if (index >= items.Count)
                    return string.Empty;

                current = items[index];
            }
            else
            {
                if (!current.TryGetProperty(part, out var next))
                {
                    return string.Empty;
                }
                current = next;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : string.Empty;
    }

    private class DeploymentInfo
    {
        public string DeploymentId { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public DateTime DeploymentTime { get; set; }
        public string CommitHash { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public List<string> ChangedFiles { get; set; } = new();
    }
}
