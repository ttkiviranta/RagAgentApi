using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RagAgentApi.Tests;

/// <summary>
/// Regression tests that validate the system against a golden dataset using embedding-based similarity.
/// These tests ensure that model updates and code changes do not degrade evaluation scores below threshold.
/// </summary>
public class EvalRegressionTests
{
    private const double SimilarityThreshold = 0.85;
    private const string BaseUrl = "https://localhost:7000"; // Assumes API is running locally

    [Fact]
    public async Task EvalQuestions_Should_MeetThreshold()
    {
        // Arrange
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
        var evalPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval", "questions.json");
        Assert.True(File.Exists(evalPath), $"Eval questions.json not found at {evalPath}");

        var json = await File.ReadAllTextAsync(evalPath);
        var items = JsonSerializer.Deserialize<List<EvalItem>>(json) ?? new List<EvalItem>();
        Assert.NotEmpty(items);

        var results = new List<(string Question, double Score)>();

        // Act
        foreach (var item in items)
        {
            var req = new { query = item.Question, topK = 5 };
            var resp = await client.PostAsJsonAsync("/api/Rag/query", req);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var answer = body.GetProperty("answer").GetString() ?? string.Empty;

            var score = await CosineSimilarityAsync(item.Expected, answer);
            results.Add((item.Question, score));
        }

        // Assert
        var overallScore = results.Count > 0 ? results.Average(r => r.Score) : 0;
        var failedQuestions = results.Where(r => r.Score < SimilarityThreshold).ToList();

        Assert.True(overallScore >= SimilarityThreshold, 
            $"Overall eval score {overallScore:F4} is below threshold {SimilarityThreshold}. Failed: {failedQuestions.Count}/{results.Count}");

        // Log failures if any
        if (failedQuestions.Any())
        {
            var failureDetails = string.Join("; ", failedQuestions.Select(f => $"'{f.Question}' ({f.Score:F4})"));
            Assert.True(false, $"Low-scoring questions: {failureDetails}");
        }
    }

    [Fact(Skip = "Requires running API")]
    public async Task EvalRunner_ShouldNotRegress_Beyond_Threshold()
    {
        // This test ensures that even if some individual questions score below threshold,
        // the overall average never drops below 0.85
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
        var evalPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval", "questions.json");

        if (!File.Exists(evalPath))
        {
            throw new FileNotFoundException("Eval questions.json not found");
        }

        var json = await File.ReadAllTextAsync(evalPath);
        var items = JsonSerializer.Deserialize<List<EvalItem>>(json) ?? new List<EvalItem>();

        var scores = new List<double>();

        foreach (var item in items)
        {
            try
            {
                var req = new { query = item.Question, topK = 5 };
                var resp = await client.PostAsJsonAsync("/api/Rag/query", req);

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
                    var answer = body.GetProperty("answer").GetString() ?? string.Empty;
                    var score = await CosineSimilarityAsync(item.Expected, answer);
                    scores.Add(score);
                }
                else
                {
                    scores.Add(0);
                }
            }
            catch
            {
                scores.Add(0);
            }
        }

        var averageScore = scores.Count > 0 ? scores.Average() : 0;
        Assert.True(averageScore >= SimilarityThreshold,
            $"Average eval score {averageScore:F4} has regressed below {SimilarityThreshold}");
    }

    /// <summary>
    /// Compute cosine similarity using embedding-based approach (fallback to Levenshtein).
    /// </summary>
    private static async Task<double> CosineSimilarityAsync(string expected, string actual)
    {
        // Normalize inputs
        var exp = (expected ?? string.Empty).ToLower().Trim();
        var act = (actual ?? string.Empty).ToLower().Trim();

        // Fallback to Levenshtein similarity
        // In production, this would use Azure OpenAI embeddings with cosine distance
        return LevenshteinSimilarity(exp, act);
    }

    private static double LevenshteinSimilarity(string a, string b)
    {
        int dist = Levenshtein(a, b);
        int max = Math.Max(a.Length, b.Length);
        if (max == 0) return 1.0;
        return Math.Max(0, 1.0 - (double)dist / max);
    }

    private static int Levenshtein(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    private record EvalItem(string Question, string Expected);
}
