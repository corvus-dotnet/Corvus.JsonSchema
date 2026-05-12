using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

// ------------------------------------------------------------------
// Basic canonicalization — property sorting
// ------------------------------------------------------------------
Console.WriteLine("=== Property Sorting ===");
Console.WriteLine();

string unordered = """{"z": 1, "a": 2, "m": 3}""";
using var doc1 = ParsedJsonDocument<JsonElement>.Parse(unordered);

byte[] canonical1 = JsonCanonicalizer.Canonicalize(doc1.RootElement);
Console.WriteLine($"Input:     {unordered}");
Console.WriteLine($"Canonical: {Encoding.UTF8.GetString(canonical1)}");
Console.WriteLine();

// ------------------------------------------------------------------
// Zero-allocation canonicalization with TryCanonicalize
// ------------------------------------------------------------------
Console.WriteLine("=== Zero-Allocation (TryCanonicalize) ===");
Console.WriteLine();

Span<byte> buffer = stackalloc byte[256];
bool success = JsonCanonicalizer.TryCanonicalize(doc1.RootElement, buffer, out int bytesWritten);
Console.WriteLine($"Success: {success}");
Console.WriteLine($"Canonical: {Encoding.UTF8.GetString(buffer.Slice(0, bytesWritten))}");
Console.WriteLine();

// ------------------------------------------------------------------
// Number formatting (ES6 Number.toString())
// ------------------------------------------------------------------
Console.WriteLine("=== ES6 Number Formatting ===");
Console.WriteLine();

string numbers = """{"large": 1e20, "small": 1e-7, "negative_zero": -0, "precise": 0.1}""";
using var doc2 = ParsedJsonDocument<JsonElement>.Parse(numbers);
byte[] canonical2 = JsonCanonicalizer.Canonicalize(doc2.RootElement);
Console.WriteLine($"Input:     {numbers}");
Console.WriteLine($"Canonical: {Encoding.UTF8.GetString(canonical2)}");
Console.WriteLine();

// ------------------------------------------------------------------
// Nested objects — recursive sorting
// ------------------------------------------------------------------
Console.WriteLine("=== Nested Object Sorting ===");
Console.WriteLine();

string nested = """{"b": {"z": 1, "a": 2}, "a": 1}""";
using var doc3 = ParsedJsonDocument<JsonElement>.Parse(nested);
byte[] canonical3 = JsonCanonicalizer.Canonicalize(doc3.RootElement);
Console.WriteLine($"Input:     {nested}");
Console.WriteLine($"Canonical: {Encoding.UTF8.GetString(canonical3)}");
Console.WriteLine();

// ------------------------------------------------------------------
// Content hashing — same logical document produces same hash
// ------------------------------------------------------------------
Console.WriteLine("=== Content Hashing ===");
Console.WriteLine();

string variant1 = """{"b": 2, "a": 1}""";
string variant2 = """{"a":1,"b":2}""";

using var docV1 = ParsedJsonDocument<JsonElement>.Parse(variant1);
using var docV2 = ParsedJsonDocument<JsonElement>.Parse(variant2);

byte[] hash1 = SHA256.HashData(JsonCanonicalizer.Canonicalize(docV1.RootElement));
byte[] hash2 = SHA256.HashData(JsonCanonicalizer.Canonicalize(docV2.RootElement));

Console.WriteLine($"Variant 1: {variant1}");
Console.WriteLine($"Variant 2: {variant2}");
Console.WriteLine($"Hash 1:    {Convert.ToHexStringLower(hash1)}");
Console.WriteLine($"Hash 2:    {Convert.ToHexStringLower(hash2)}");
Console.WriteLine($"Match:     {hash1.AsSpan().SequenceEqual(hash2)}");
