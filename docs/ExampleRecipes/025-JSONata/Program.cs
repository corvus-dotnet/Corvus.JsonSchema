using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;
using JSONata.Expressions;

// ------------------------------------------------------------------
// Sample data
// ------------------------------------------------------------------
string orderJson =
    """
    {
        "customer": "Alice",
        "items": [
            {"name": "Widget", "price": 25.00, "quantity": 4},
            {"name": "Gadget", "price": 99.99, "quantity": 1},
            {"name": "Doohickey", "price": 4.50, "quantity": 10}
        ]
    }
    """;

using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(orderJson);
var evaluator = JsonataEvaluator.Default;

// ------------------------------------------------------------------
// 1. EvaluateToString — the simplest API
// ------------------------------------------------------------------
Console.WriteLine("=== EvaluateToString (string in, string out) ===");
Console.WriteLine();

string? result = evaluator.EvaluateToString("customer", orderJson);
Console.WriteLine($"Customer: {result}");

result = evaluator.EvaluateToString("$sum(items.(price * quantity))", orderJson);
Console.WriteLine($"Order total: {result}");
Console.WriteLine();

// ------------------------------------------------------------------
// 2. Path navigation and filtering
// ------------------------------------------------------------------
Console.WriteLine("=== Path navigation and filtering ===");
Console.WriteLine();

// Simple property path
Console.WriteLine($"First item name: {evaluator.EvaluateToString("items[0].name", orderJson)}");

// Wildcard
Console.WriteLine($"All names: {evaluator.EvaluateToString("items.name", orderJson)}");

// Predicate filter
Console.WriteLine($"Expensive items (price > 20): {evaluator.EvaluateToString(
    "items[price > 20].name", orderJson)}");

// Computed expression
Console.WriteLine($"Line totals: {evaluator.EvaluateToString(
    "items.(name & \": $\" & price * quantity)", orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 3. Built-in functions
// ------------------------------------------------------------------
Console.WriteLine("=== Built-in functions ===");
Console.WriteLine();

// Aggregation
Console.WriteLine($"Sum of prices: {evaluator.EvaluateToString("$sum(items.price)", orderJson)}");
Console.WriteLine($"Average price: {evaluator.EvaluateToString("$average(items.price)", orderJson)}");
Console.WriteLine($"Max price:     {evaluator.EvaluateToString("$max(items.price)", orderJson)}");
Console.WriteLine($"Item count:    {evaluator.EvaluateToString("$count(items)", orderJson)}");

// String functions
Console.WriteLine($"Uppercase:  {evaluator.EvaluateToString("$uppercase(customer)", orderJson)}");
Console.WriteLine($"Lowercase:  {evaluator.EvaluateToString("$lowercase(customer)", orderJson)}");
Console.WriteLine($"Joined:     {evaluator.EvaluateToString("$join(items.name, \", \")", orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 4. Higher-order functions ($map, $filter, $reduce, $sort)
// ------------------------------------------------------------------
Console.WriteLine("=== Higher-order functions ===");
Console.WriteLine();

string mapExpr = """$map(items, function($v) { $v.name & " x" & $v.quantity })""";
Console.WriteLine($"$map:    {evaluator.EvaluateToString(mapExpr, orderJson)}");

string filterExpr = """$filter(items, function($v) { $v.price >= 10 })""";
Console.WriteLine($"$filter: {evaluator.EvaluateToString(filterExpr, orderJson)}");

string reduceExpr = """$reduce(items, function($acc, $v) { $acc + $v.price * $v.quantity }, 0)""";
Console.WriteLine($"$reduce: {evaluator.EvaluateToString(reduceExpr, orderJson)}");

string sortExpr = """$sort(items, function($a, $b) { $a.price > $b.price }).name""";
Console.WriteLine($"$sort:   {evaluator.EvaluateToString(sortExpr, orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 5. Object construction and transformation
// ------------------------------------------------------------------
Console.WriteLine("=== Object construction ===");
Console.WriteLine();

string transform =
    """
    {
        "orderSummary": customer & "'s order",
        "lineItems": items.{
            "product": name,
            "lineTotal": price * quantity
        },
        "grandTotal": $sum(items.(price * quantity))
    }
    """;

Console.WriteLine($"Transform:\n{evaluator.EvaluateToString(transform, orderJson)}");
Console.WriteLine();

// ------------------------------------------------------------------
// 6. Variable bindings (values)
// ------------------------------------------------------------------
Console.WriteLine("=== Variable bindings ===");
Console.WriteLine();

using JsonWorkspace workspace = JsonWorkspace.Create();

var bindings = new Dictionary<string, JsonataBinding>
{
    ["taxRate"] = JsonataBinding.FromValue(0.2),
    ["currency"] = JsonataBinding.FromValue("GBP"),
};

JsonElement taxResult = evaluator.Evaluate(
    "$sum(items.(price * quantity)) * (1 + $taxRate) & \" \" & $currency",
    dataDoc.RootElement,
    workspace,
    bindings);

Console.WriteLine($"Total with tax: {taxResult}");
Console.WriteLine();

// ------------------------------------------------------------------
// 7. Custom C# function bindings
// ------------------------------------------------------------------
Console.WriteLine("=== Custom function bindings ===");
Console.WriteLine();

var fnBindings = new Dictionary<string, JsonataBinding>
{
    // Simple double->double function (zero-allocation shorthand)
    ["roundUp"] = JsonataBinding.FromFunction((double v) => Math.Ceiling(v)),

    // Binary double function
    ["hypot"] = JsonataBinding.FromFunction((double a, double b) => Math.Sqrt(a * a + b * b)),
};

JsonElement fnResult = evaluator.Evaluate(
    "$roundUp($sum(items.(price * quantity)) * 1.2)",
    dataDoc.RootElement,
    workspace,
    fnBindings);
Console.WriteLine($"Rounded total with 20% tax: {fnResult}");

fnResult = evaluator.Evaluate(
    "$hypot(3, 4)",
    dataDoc.RootElement,
    workspace,
    fnBindings);
Console.WriteLine($"$hypot(3, 4): {fnResult}");

Console.WriteLine();

// ------------------------------------------------------------------
// 8. Recursion depth and timeout controls
// ------------------------------------------------------------------
Console.WriteLine("=== Safety controls ===");
Console.WriteLine();

try
{
    // This expression recurses deeply — the depth limit will catch it
    evaluator.Evaluate(
        "($f := function($n) { $n <= 1 ? 1 : $n * $f($n - 1) }; $f(10))",
        dataDoc.RootElement,
        maxDepth: 5,
        timeLimitMs: 0);
}
catch (JsonataException ex)
{
    Console.WriteLine($"Depth limit caught: {ex.Code} at position {ex.Position}");
}

try
{
    // Timeout for expensive expressions
    evaluator.Evaluate(
        "($f := function($n) { $n <= 1 ? 1 : $f($n - 1) + $f($n - 2) }; $f(100))",
        dataDoc.RootElement,
        maxDepth: 500,
        timeLimitMs: 100);
}
catch (JsonataException ex)
{
    Console.WriteLine($"Timeout caught: {ex.Code}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 9. Source-generated expression (compiled at build time)
// ------------------------------------------------------------------
Console.WriteLine("=== Source-generated expression ===");
Console.WriteLine();

JsonElement sgResult = TotalPrice.Evaluate(dataDoc.RootElement, workspace);
Console.WriteLine($"Total price (source-generated): {sgResult}");
