using System;
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

var json = System.Text.Encoding.UTF8.GetBytes("""{"Account":{"Order":[{"Product":[{"Price":34.45,"Quantity":2},{"Price":21.67,"Quantity":1}]},{"Product":[{"Price":34.45,"Quantity":4},{"Price":107.99,"Quantity":1}]}]}}""");
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var data = doc.RootElement;
var evaluator = new JsonataEvaluator();
using var workspace = JsonWorkspace.Create();

// Warm up
evaluator.Evaluate("Account.Order.Product.Price", data, workspace);
workspace.Reset();

// Measure
long before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100; i++)
{
    workspace.Reset();
    evaluator.Evaluate("Account.Order.Product.Price", data, workspace);
}
long after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"DeepPath: {(after - before) / 100} bytes/iteration");

before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100; i++)
{
    workspace.Reset();
    evaluator.Evaluate("Account.Order.Product.(Price * Quantity)", data, workspace);
}
after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"MapArithmetic: {(after - before) / 100} bytes/iteration");

before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100; i++)
{
    workspace.Reset();
    evaluator.Evaluate("Account.Order.Product.{`Product Name`: Price}", data, workspace);
}
after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"GroupByObject: {(after - before) / 100} bytes/iteration");

before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < 100; i++)
{
    workspace.Reset();
    evaluator.Evaluate("""[Account.Order.Product.{"name": `Product Name`, "total": Price * Quantity}]""", data, workspace);
}
after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"ArrayOfObjects: {(after - before) / 100} bytes/iteration");
