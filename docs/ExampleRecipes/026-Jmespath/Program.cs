using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using JMESPath.Expressions;

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
var evaluator = JMESPathEvaluator.Default;

// ------------------------------------------------------------------
// 1. Basic expressions — identifiers and sub-expressions
// ------------------------------------------------------------------
Console.WriteLine("=== Basic expressions ===");
Console.WriteLine();

using JsonWorkspace ws1 = JsonWorkspace.Create();
JsonElement customer = evaluator.Search("customer", dataDoc.RootElement, ws1);
Console.WriteLine($"Customer: {customer}");

JsonElement firstName = evaluator.Search("items[0].name", dataDoc.RootElement, ws1);
Console.WriteLine($"First item name: {firstName}");

Console.WriteLine();

// ------------------------------------------------------------------
// 2. List projections (wildcard [*])
// ------------------------------------------------------------------
Console.WriteLine("=== List projections ===");
Console.WriteLine();

string peopleJson =
    """
    {
      "people": [
        {"first": "James", "last": "d"},
        {"first": "Jacob", "last": "e"},
        {"first": "Jayden", "last": "f"},
        {"missing": "different"}
      ]
    }
    """;

using var peopleDoc = ParsedJsonDocument<JsonElement>.Parse(peopleJson);
JsonElement allFirstNames = evaluator.Search("people[*].first", peopleDoc.RootElement);
Console.WriteLine($"All first names: {allFirstNames}");

Console.WriteLine();

// ------------------------------------------------------------------
// 3. Object projections (wildcard *)
// ------------------------------------------------------------------
Console.WriteLine("=== Object projections ===");
Console.WriteLine();

string opsJson =
    """
    {
      "ops": {
        "functionA": {"numArgs": 2},
        "functionB": {"numArgs": 3},
        "functionC": {"variadic": true}
      }
    }
    """;

using var opsDoc = ParsedJsonDocument<JsonElement>.Parse(opsJson);
JsonElement numArgsList = evaluator.Search("ops.*.numArgs", opsDoc.RootElement);
Console.WriteLine($"ops.*.numArgs: {numArgsList}");

Console.WriteLine();

// ------------------------------------------------------------------
// 4. Flatten projections ([])
// ------------------------------------------------------------------
Console.WriteLine("=== Flatten projections ===");
Console.WriteLine();

string reservationsJson =
    """
    {
      "reservations": [
        {
          "instances": [
            {"state": "running"},
            {"state": "stopped"}
          ]
        },
        {
          "instances": [
            {"state": "terminated"},
            {"state": "running"}
          ]
        }
      ]
    }
    """;

using var resDoc = ParsedJsonDocument<JsonElement>.Parse(reservationsJson);
JsonElement allStates = evaluator.Search(
    "reservations[].instances[].state", resDoc.RootElement);
Console.WriteLine($"All states (flattened): {allStates}");

Console.WriteLine();

// ------------------------------------------------------------------
// 5. Filter projections ([? ... ])
// ------------------------------------------------------------------
Console.WriteLine("=== Filter projections ===");
Console.WriteLine();

string machinesJson =
    """
    {
      "machines": [
        {"name": "a", "state": "running"},
        {"name": "b", "state": "stopped"},
        {"name": "c", "state": "running"}
      ]
    }
    """;

using var machinesDoc = ParsedJsonDocument<JsonElement>.Parse(machinesJson);
JsonElement runningNames = evaluator.Search(
    "machines[?state=='running'].name", machinesDoc.RootElement);
Console.WriteLine($"Running machines: {runningNames}");

Console.WriteLine();

// ------------------------------------------------------------------
// 6. Slicing
// ------------------------------------------------------------------
Console.WriteLine("=== Slicing ===");
Console.WriteLine();

string numbersJson = "[0, 1, 2, 3, 4, 5, 6, 7, 8, 9]";

using var numbersDoc = ParsedJsonDocument<JsonElement>.Parse(numbersJson);
JsonElement firstHalf = evaluator.Search("[:5]", numbersDoc.RootElement);
Console.WriteLine($"First half [0:5]:  {firstHalf}");

JsonElement evens = evaluator.Search("[::2]", numbersDoc.RootElement);
Console.WriteLine($"Even indices [::2]: {evens}");

JsonElement reversed = evaluator.Search("[::-1]", numbersDoc.RootElement);
Console.WriteLine($"Reversed [::-1]:    {reversed}");

Console.WriteLine();

// ------------------------------------------------------------------
// 7. Pipe expressions
// ------------------------------------------------------------------
Console.WriteLine("=== Pipe expressions ===");
Console.WriteLine();

JsonElement firstOfProjection = evaluator.Search(
    "people[*].first | [0]", peopleDoc.RootElement);
Console.WriteLine($"First of projection (pipe): {firstOfProjection}");

Console.WriteLine();

// ------------------------------------------------------------------
// 8. MultiSelect — lists and hashes
// ------------------------------------------------------------------
Console.WriteLine("=== MultiSelect ===");
Console.WriteLine();

string multiJson =
    """
    {
      "people": [
        {"name": "James", "state": {"name": "Virginia"}},
        {"name": "Jacob", "state": {"name": "Texas"}},
        {"name": "Jayden", "state": {"name": "Florida"}}
      ]
    }
    """;

using var multiDoc = ParsedJsonDocument<JsonElement>.Parse(multiJson);

JsonElement selectList = evaluator.Search(
    "people[*].[name, state.name]", multiDoc.RootElement);
Console.WriteLine($"MultiSelect list: {selectList}");

JsonElement selectHash = evaluator.Search(
    "people[*].{Name: name, State: state.name}", multiDoc.RootElement);
Console.WriteLine($"MultiSelect hash: {selectHash}");

Console.WriteLine();

// ------------------------------------------------------------------
// 9. Built-in functions
// ------------------------------------------------------------------
Console.WriteLine("=== Functions ===");
Console.WriteLine();

using JsonWorkspace ws2 = JsonWorkspace.Create();

JsonElement itemCount = evaluator.Search("length(items)", dataDoc.RootElement, ws2);
Console.WriteLine($"Item count: {itemCount}");

JsonElement priceSum = evaluator.Search("sum(items[*].price)", dataDoc.RootElement, ws2);
Console.WriteLine($"Sum of prices: {priceSum}");

JsonElement maxPrice = evaluator.Search("max(items[*].price)", dataDoc.RootElement, ws2);
Console.WriteLine($"Max price: {maxPrice}");

JsonElement sorted = evaluator.Search(
    "sort_by(items, &price)[*].name", dataDoc.RootElement, ws2);
Console.WriteLine($"Sorted by price: {sorted}");

JsonElement mostExpensive = evaluator.Search(
    "max_by(items, &price).name", dataDoc.RootElement, ws2);
Console.WriteLine($"Most expensive: {mostExpensive}");

string arrayJson =
    """
    {"myarray": ["foo", "foobar", "barfoo", "bar", "baz", "barbaz", "barfoobaz"]}
    """;

using var arrayDoc = ParsedJsonDocument<JsonElement>.Parse(arrayJson);
JsonElement withFoo = evaluator.Search(
    "myarray[?contains(@, 'foo')]", arrayDoc.RootElement, ws2);
Console.WriteLine($"Contains 'foo': {withFoo}");

Console.WriteLine();

// ------------------------------------------------------------------
// 10. Source-generated expression (compiled at build time)
// ------------------------------------------------------------------
Console.WriteLine("=== Source-generated expression ===");
Console.WriteLine();

using JsonWorkspace ws3 = JsonWorkspace.Create();
JsonElement sgResult = TotalPrice.Evaluate(dataDoc.RootElement, ws3);
Console.WriteLine($"Sum of prices (source-generated): {sgResult}");
