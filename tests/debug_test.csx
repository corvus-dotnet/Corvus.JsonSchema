using System;
using System.Text.Json;
using Corvus.Text.Json.Jsonata;

var json = """{"foo":{"blah":[{"baz":{"fud":"hello"}},{"baz":{"fud":"world"},"bazz":"gotcha"}],"bar":42}}""";
using var doc = JsonDocument.Parse(json);

// Test 1: foo.blah[0].baz.fud => should be "hello"
try {
    var e = new JsonataEvaluator();
    var r = e.Evaluate("foo.blah[0].baz.fud", doc.RootElement);
    Console.WriteLine($"foo.blah[0].baz.fud => {(r.ValueKind == JsonValueKind.Undefined ? "undefined" : r.GetRawText())}");
} catch (Exception ex) { Console.WriteLine($"ERROR: {ex.Message}"); }

// Test 2: (foo.blah)[0].baz.fud => should be "hello"  
try {
    var e = new JsonataEvaluator();
    var r = e.Evaluate("(foo.blah)[0].baz.fud", doc.RootElement);
    Console.WriteLine($"(foo.blah)[0].baz.fud => {(r.ValueKind == JsonValueKind.Undefined ? "undefined" : r.GetRawText())}");
} catch (Exception ex) { Console.WriteLine($"ERROR: {ex.Message}"); }
