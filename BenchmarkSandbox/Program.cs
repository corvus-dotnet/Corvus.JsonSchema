// Allocation-tracing test for $match RT performance.

using System;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

using JsonElement = Corvus.Text.Json.JsonElement;

// Test data matching the benchmark
const string DataJson = """
    {
        "text": "Call me at 555-123-4567 or 555-987-6543. My other number is 555-000-1111. Office: 555-222-3333."
    }
    """;

using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(DataJson));
var data = doc.RootElement;

var evaluator = new JsonataEvaluator();

(string Name, string Expr)[] tests = [
    ("$match (4 matches)",      @"$match(text, /\d{3}-\d{3}-\d{4}/)"),
    ("$match (groups)",         @"$match(text, /(\d{3})-(\d{3})-(\d{4})/)"),
    ("$match (1 match, limit)", @"$match(text, /\d{3}-\d{3}-\d{4}/, 1)"),
    ("$replace",                @"$replace(text, /\d{3}-\d{3}-\d{4}/, ""***-***-****"")"),
    ("$contains",               @"$contains(text, /\d{3}-\d{3}-\d{4}/)"),
    ("$split",                  @"$split(text, /\d{3}-\d{3}-\d{4}/)"),
];

Console.WriteLine("=== RT $match/$replace Allocation Tracing ===");
Console.WriteLine();
Console.WriteLine($"{"Expression",-30} {"Total Alloc",14} {"Result"}");
Console.WriteLine(new string('-', 80));

foreach (var (name, expr) in tests)
{
    using var workspace = JsonWorkspace.Create();

    // Pre-warm
    for (int i = 0; i < 3; i++)
    {
        workspace.Reset();
        evaluator.Evaluate(expr, data, workspace);
    }

    workspace.Reset();
    GC.Collect(2, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);

    long before = GC.GetAllocatedBytesForCurrentThread();
    workspace.Reset();
    var result = evaluator.Evaluate(expr, data, workspace);
    long after = GC.GetAllocatedBytesForCurrentThread();
    long total = after - before;

    string resultInfo = result.ValueKind switch
    {
        JsonValueKind.Array => $"array[{result.GetArrayLength()}]",
        JsonValueKind.Object => $"object({result.GetRawText()[..Math.Min(60, result.GetRawText().Length)]}...)",
        JsonValueKind.String => $"string({result.GetString()?[..Math.Min(40, result.GetString()!.Length)]})",
        JsonValueKind.True or JsonValueKind.False => $"bool={result.GetRawText()}",
        _ => result.ValueKind.ToString(),
    };

    Console.WriteLine($"{name,-30} {total,14:N0} {resultInfo}");
}

// Detailed per-call breakdown: run $match N times and check scaling
Console.WriteLine();
Console.WriteLine("=== Scaling test: $match on varying match counts ===");

string[] matchTexts = [
    """{"text":"no matches here"}""",
    """{"text":"one 555-123-4567 match"}""",
    """{"text":"two 555-123-4567 and 555-987-6543"}""",
    """{"text":"Call me at 555-123-4567 or 555-987-6543. My other number is 555-000-1111. Office: 555-222-3333."}""",
];

string matchExpr = @"$match(text, /\d{3}-\d{3}-\d{4}/)";

foreach (string json in matchTexts)
{
    using var matchDoc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
    var matchData = matchDoc.RootElement;
    using var workspace = JsonWorkspace.Create();

    // Pre-warm
    for (int i = 0; i < 3; i++)
    {
        workspace.Reset();
        evaluator.Evaluate(matchExpr, matchData, workspace);
    }

    workspace.Reset();
    GC.Collect(2, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);

    long before = GC.GetAllocatedBytesForCurrentThread();
    workspace.Reset();
    var result = evaluator.Evaluate(matchExpr, matchData, workspace);
    long after = GC.GetAllocatedBytesForCurrentThread();
    long total = after - before;

    int matchCount = result.ValueKind == JsonValueKind.Array ? result.GetArrayLength()
                   : result.ValueKind == JsonValueKind.Object ? 1
                   : 0;

    long perMatch = matchCount > 0 ? total / matchCount : 0;
    Console.WriteLine($"  {matchCount} matches: {total,8:N0} bytes total, {perMatch,6:N0} B/match");
}

// Iteration scaling: run $match 1000 times to see amortized cost
Console.WriteLine();
Console.WriteLine("=== Amortized cost: 1000 iterations of $match (4 matches) ===");
{
    using var workspace = JsonWorkspace.Create();

    // Pre-warm
    for (int i = 0; i < 3; i++)
    {
        workspace.Reset();
        evaluator.Evaluate(matchExpr, data, workspace);
    }

    GC.Collect(2, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);

    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++)
    {
        workspace.Reset();
        evaluator.Evaluate(matchExpr, data, workspace);
    }
    long after = GC.GetAllocatedBytesForCurrentThread();
    long total = after - before;
    Console.WriteLine($"  Total: {total:N0} bytes, per-iteration: {total / 1000.0:F1} bytes");
}
