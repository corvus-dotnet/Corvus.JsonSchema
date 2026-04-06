using System;
using System.Text.Json;
using Corvus.Text.Json.Jsonata;

var evaluator = new JsonataEvaluator();

// Test 1: Simple $replace with regex + string replacement (should work)
Console.WriteLine("Test 1: $replace with regex string");
try {
    var r1 = evaluator.Evaluate("$replace(\"Hello World\", /World/, \"Earth\")");
    Console.WriteLine($"  Result: {r1.GetRawText()}");
} catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }

// Test 2: $replace with regex + function replacement (simple)
Console.WriteLine("Test 2: $replace with regex + function (constant)");
try {
    var r2 = evaluator.Evaluate("$replace(\"Hello World\", /World/, function($m) { \"Earth\" })");
    Console.WriteLine($"  Result: {r2.GetRawText()}");
} catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }

// Test 3: $replace with regex + function using $m.match  
Console.WriteLine("Test 3: $replace with regex + function using $m.match");
try {
    var r3 = evaluator.Evaluate("$replace(\"Hello World\", /World/, function($m) { $uppercase($m.match) })");
    Console.WriteLine($"  Result: {r3.GetRawText()}");
} catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }

// Test 4: Same but with $match as parameter name
Console.WriteLine("Test 4: $replace with regex + function($match) using $match.match");
try {
    var r4 = evaluator.Evaluate("$replace(\"Hello World\", /World/, function($match) { $uppercase($match.match) })");
    Console.WriteLine($"  Result: {r4.GetRawText()}");
} catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }

// Test 5: $match function
Console.WriteLine("Test 5: $match");
try {
    var r5 = evaluator.Evaluate("$match(\"Hello World\", /World/)");
    Console.WriteLine($"  Result: {(r5.ValueKind == JsonValueKind.Undefined ? "undefined" : r5.GetRawText())}");
} catch (Exception ex) { Console.WriteLine($"  Error: {ex.Message}"); }
