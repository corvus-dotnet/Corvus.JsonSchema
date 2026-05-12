using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;
using JsonLogic.Rules;

// ------------------------------------------------------------------
// Sample data
// ------------------------------------------------------------------
string orderJson =
    """
    {
        "order": {
            "customer": "Alice",
            "total": 150,
            "items": ["widget", "gadget"]
        }
    }
    """;

using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(orderJson);

// ------------------------------------------------------------------
// 1. EvaluateToString — the simplest API
// ------------------------------------------------------------------
Console.WriteLine("=== EvaluateToString (string in, string out) ===");
Console.WriteLine();

string? result = JsonLogicEvaluator.Default.EvaluateToString(
    """{"if": [{">=": [{"var": "order.total"}, 100]}, "VIP", "Standard"]}""",
    orderJson);

Console.WriteLine($"Customer tier: {result}");
Console.WriteLine();

// ------------------------------------------------------------------
// 2. Evaluate with parsed documents and workspace
// ------------------------------------------------------------------
Console.WriteLine("=== Evaluate with JsonElement ===");
Console.WriteLine();

using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"*": [{"var": "order.total"}, 0.9]}""");

using JsonWorkspace workspace = JsonWorkspace.Create();
JsonLogicRule rule = new(ruleDoc.RootElement);

JsonElement discounted = JsonLogicEvaluator.Default.Evaluate(
    in rule, dataDoc.RootElement, workspace);

Console.WriteLine($"10% discount on {dataDoc.RootElement.GetProperty("order").GetProperty("total")}: {discounted}");
Console.WriteLine();

// ------------------------------------------------------------------
// 3. Data access with var, missing, and missing_some
// ------------------------------------------------------------------
Console.WriteLine("=== Data access operators ===");
Console.WriteLine();

// Nested property access
Console.WriteLine($"customer: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"var": "order.customer"}""", orderJson)}");

// Default value when property is missing
Console.WriteLine($"missing field (with default): {JsonLogicEvaluator.Default.EvaluateToString(
    """{"var": ["order.discount", 0]}""", orderJson)}");

// Check which fields are missing
Console.WriteLine($"missing: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"missing": ["order.customer", "order.discount", "order.total"]}""", orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 4. Comparison and logic operators
// ------------------------------------------------------------------
Console.WriteLine("=== Comparison and logic ===");
Console.WriteLine();

// Between (exclusive): 50 < total < 200
Console.WriteLine($"50 < total < 200: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"<": [50, {"var": "order.total"}, 200]}""", orderJson)}");

// Logical AND
Console.WriteLine($"total >= 100 AND has items: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"and": [{">=": [{"var": "order.total"}, 100]}, {"var": "order.items"}]}""", orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 5. Array operations
// ------------------------------------------------------------------
Console.WriteLine("=== Array operations ===");
Console.WriteLine();

string productsJson =
    """
    {
        "products": [
            {"name": "Widget", "price": 25, "inStock": true},
            {"name": "Gadget", "price": 75, "inStock": false},
            {"name": "Doohickey", "price": 150, "inStock": true}
        ]
    }
    """;

// Filter: in-stock items over $50
Console.WriteLine($"In-stock over $50: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"filter": [{"var": "products"}, {"and": [{"var": "inStock"}, {">": [{"var": "price"}, 50]}]}]}""",
    productsJson)}");

// Map: extract names
Console.WriteLine($"Names: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"map": [{"var": "products"}, {"var": "name"}]}""",
    productsJson)}");

// Reduce: sum prices
Console.WriteLine($"Total price: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"reduce": [{"var": "products"}, {"+": [{"var": "accumulator"}, {"var": "current.price"}]}, 0]}""",
    productsJson)}");

// All/Some/None
Console.WriteLine($"All in stock: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"all": [{"var": "products"}, {"var": "inStock"}]}""",
    productsJson)}");

Console.WriteLine($"Some in stock: {JsonLogicEvaluator.Default.EvaluateToString(
    """{"some": [{"var": "products"}, {"var": "inStock"}]}""",
    productsJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 6. Custom operators
// ------------------------------------------------------------------
Console.WriteLine("=== Custom operators ===");
Console.WriteLine();

// Define a custom "percent" operator: {"percent": [value, percentage]}
var customEvaluator = new JsonLogicEvaluator(
    new Dictionary<string, IOperatorCompiler>
    {
        ["percent"] = new PercentOperator(),
    });

Console.WriteLine($"20% of 250: {customEvaluator.EvaluateToString(
    """{"percent": [250, 20]}""",
    "{}")}");

Console.WriteLine($"15% of order total: {customEvaluator.EvaluateToString(
    """{"percent": [{"var": "order.total"}, 15]}""",
    orderJson)}");

Console.WriteLine();

// ------------------------------------------------------------------
// 7. Source-generated rule (compiled at build time)
// ------------------------------------------------------------------
Console.WriteLine("=== Source-generated rule ===");
Console.WriteLine();

JsonElement sgResult = DiscountRule.Evaluate(dataDoc.RootElement, workspace);
Console.WriteLine($"Discount rule (total=150): {sgResult}");

// Try with a smaller order
using var smallOrderDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"order": {"total": 50}}""");
JsonElement sgResult2 = DiscountRule.Evaluate(smallOrderDoc.RootElement, workspace);
Console.WriteLine($"Discount rule (total=50):  {sgResult2}");

// ------------------------------------------------------------------
// Custom operator implementation
// ------------------------------------------------------------------
file class PercentOperator : IOperatorCompiler
{
    public RuleEvaluator Compile(RuleEvaluator[] operands)
    {
        RuleEvaluator valueEval = operands[0];
        RuleEvaluator percentEval = operands[1];

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            EvalResult value = valueEval(in data, ws);
            EvalResult percent = percentEval(in data, ws);

            if (value.TryGetDouble(out double v) && percent.TryGetDouble(out double p))
            {
                return EvalResult.FromDouble(v * p / 100.0);
            }

            return EvalResult.FromDouble(0);
        };
    }
}
