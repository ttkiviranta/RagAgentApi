using System.Net.Http.Json;
using System.Text.Json;

Console.WriteLine("RagAgentApi Eval Runner");

var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7000";
var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

var questionsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "eval", "questions.json");
if (!File.Exists(questionsPath))
{
    Console.WriteLine($"Eval file not found: {questionsPath}");
    return 1;
}

var json = await File.ReadAllTextAsync(questionsPath);
var items = JsonSerializer.Deserialize<List<EvalItem>>(json) ?? new List<EvalItem>();

var scores = new List<double>();

foreach (var item in items)
{
    Console.WriteLine($"Question: {item.q}");
    var req = new { query = item.q, topK = 5 };
    var resp = await client.PostAsJsonAsync("/api/Rag/query", req);
    if (!resp.IsSuccessStatusCode)
    {
        Console.WriteLine($"  Request failed: {resp.StatusCode}");
        scores.Add(0);
        continue;
    }

    var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
    var answer = body.GetProperty("answer").GetString() ?? string.Empty;
    Console.WriteLine($"  Answer: {answer.Substring(0, Math.Min(200, answer.Length)).Replace('\n', ' ')}...");

    var score = Similarity(item.expected, answer);
    scores.Add(score);
    Console.WriteLine($"  Score: {score:F3}");
}

var overall = scores.Count > 0 ? scores.Average() : 0;
Console.WriteLine($"Overall score: {overall:F3}");
return 0;

static double Similarity(string a, string b)
{
    // Simple normalized Levenshtein-based similarity
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
