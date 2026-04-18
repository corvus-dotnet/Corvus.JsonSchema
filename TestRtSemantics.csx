using System;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

var evaluator = new JsonataEvaluator();
using var workspace = JsonWorkspace.Create();

void Test(string expr, string data) {
    using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(data));
    var r = evaluator.Evaluate(Encoding.UTF8.GetBytes(expr), doc.RootElement, workspace);
    workspace.Reset();
    Console.WriteLine(r.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "undefined" : r.GetRawText());
}

// 1: tags are arrays themselves
Test("items[type=\"a\"].tags", "{\"items\":[{\"type\":\"a\",\"tags\":[1,2]},{\"type\":\"a\",\"tags\":[3]},{\"type\":\"b\",\"tags\":[4]}]}");
// 2: products is array of objects
Test("items[type=\"a\"].products.name", "{\"items\":[{\"type\":\"a\",\"products\":[{\"name\":\"x\"},{\"name\":\"y\"}]},{\"type\":\"b\",\"products\":[{\"name\":\"z\"}]}]}");
// 3: deep chain
Test("orders[status=\"shipped\"].lines.product.sku", "{\"orders\":[{\"status\":\"shipped\",\"lines\":[{\"product\":{\"sku\":\"A1\"}},{\"product\":{\"sku\":\"A2\"}}]},{\"status\":\"pending\",\"lines\":[{\"product\":{\"sku\":\"B1\"}}]}]}");
