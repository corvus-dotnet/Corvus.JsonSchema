// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace JsonPointerBenchmarks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Measures deriving the JSON Pointer of an element relative to its document root by walking
/// the metadata database to its ancestors. The span overloads must report 0 B allocated; the
/// string overload allocates only the returned string.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkGetJsonPointer
{
    private static readonly byte[] Utf8Buffer = new byte[128];
    private static readonly char[] CharBuffer = new char[128];

    private ParsedJsonDocument<JsonElement>? document;
    private JsonElement element;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<JsonElement>.Parse(
            """{"store":{"book":[{"title":"A","price":10},{"title":"B","price":5},{"title":"C","price":7},{"m~n":{"x/y":true},"title":"D","price":9}]}}""");
        this.element = this.document.RootElement["store"u8]["book"u8][3]["m~n"]["x/y"];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool TryGetJsonPointerUtf8()
    {
        return this.element.TryGetJsonPointer(Utf8Buffer, out _);
    }

    [Benchmark]
    public bool TryGetJsonPointerChars()
    {
        return this.element.TryGetJsonPointer(CharBuffer, out _);
    }

    [Benchmark]
    public string GetJsonPointerString()
    {
        return this.element.GetJsonPointer();
    }
}