using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

using JsonElement = Corvus.Text.Json.JsonElement;

string dataJson = File.ReadAllText(@"D:\source\corvus-dotnet\Corvus.JsonSchema\Jsonata-Test-Suite\test\test-suite\datasets\dataset5.json");

using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(dataJson));
var data = doc.RootElement;

var evaluator = new JsonataEvaluator();

// Benchmark expressions
(string Name, string Expr)[] benchmarks = [
    ("DeepPath",       "Account.Order.Product.Price"),
    ("MapArithmetic",  "Account.Order.Product.(Price * Quantity)"),
    ("SortPath",       "Account.Order.Product^(Price).SKU"),
    ("SumAggregate",   "$sum(Account.Order.Product.(Price * Quantity))"),
    ("StringLookup",   "Account.Order.Product.`Product Name`"),
    ("ScalarPath",     "Account.`Account Name`"),
    ("ArrayPath",      "Account.Order.OrderID"),
    ("SortFull",       "Account.Order.Product^(Price)"),
];

const int warmupIters = 500;
const int benchIters = 5000;

Console.WriteLine($"{"Benchmark",-20} {"Ops/sec",12} {"Mean (us)",12} {"Alloc (B)",12}");
Console.WriteLine(new string('-', 60));

foreach (var (name, expr) in benchmarks)
{
    // Warmup
    for (int i = 0; i < warmupIters; i++)
    {
        evaluator.Evaluate(expr, data);
    }

    // Force GC
    GC.Collect(2, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);

    long allocBefore = GC.GetTotalAllocatedBytes(true);
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < benchIters; i++)
    {
        evaluator.Evaluate(expr, data);
    }

    sw.Stop();
    long allocAfter = GC.GetTotalAllocatedBytes(true);

    double totalMs = sw.Elapsed.TotalMilliseconds;
    double meanUs = (totalMs / benchIters) * 1000.0;
    double opsPerSec = benchIters / (totalMs / 1000.0);
    long allocPerOp = (allocAfter - allocBefore) / benchIters;

    Console.WriteLine($"{name,-20} {opsPerSec,12:N0} {meanUs,12:F2} {allocPerOp,12:N0}");
}

Console.WriteLine();

// Parallel correctness check
string expected = """["0406634348","0406654608","040657863","0406654603"]""";
int pass = 0, fail = 0;
System.Threading.Tasks.Parallel.For(0, 500, i =>
{
    var result = evaluator.Evaluate("Account.Order.Product^(Price).SKU", data);
    string resultJson = result.GetRawText();
    if (resultJson == expected)
        System.Threading.Interlocked.Increment(ref pass);
    else
    {
        int f = System.Threading.Interlocked.Increment(ref fail);
        if (f <= 5) Console.WriteLine($"  FAIL run {i}: {resultJson}");
    }
});
Console.WriteLine($"Parallel sort correctness: Pass={pass}, Fail={fail}");
