using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RagAgentApi.Tests;

public class EvalRegressionTests
{
    [Fact]
    public async Task EvalQuestions_Should_MeetThreshold()
    {
        // Arrange
        var baseUrl = "https://localhost:7000"; // Assumes API is running locally for tests
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

        var evalPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval", "questions.json");
        Assert.True(File.Exists(evalPath), "Eval questions.json not found");

        var json = await File.ReadAllTextAsync(evalPath);
        var items = JsonSerializer.Deserialize<List<EvalItem>>(json) ?? new List<EvalItem>();

        // Act & Assert
        double threshold = 0.8;
        foreach (var item in items)
        {
            var req = new { query = item.q, topK = 5 };
            var resp = await client.PostAsJsonAsync("/api/Rag/query", req);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var answer = body.GetProperty("answer").GetString() ?? string.Empty;

            var score = Similarity(item.expected, answer);
            Assert.True(score >= threshold, $"Eval failed for '{item.q}' score={score:F3}");
        }
    }

    static double Similarity(string a, string b)
    {
        int dist = Levenshtein(a ?? string.Empty, b ?? string.Empty);
        int max = Math.Max(a.Length, b.Length);
        if (max == 0) return 1.0;
        return 1.0 - (double)dist / max;
    }

    static int Levenshtein(string s, string t)
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

    record EvalItem(string q, string expected);
}
