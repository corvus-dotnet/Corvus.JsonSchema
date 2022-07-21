// <copyright file="ConstructFromJsonElement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class ConstructFromJsonElement
{
    private readonly JsonDocument objectDocument = JsonDocument.Parse("{}");
    private readonly JsonDocument arrayDocument = JsonDocument.Parse("[]");
    private readonly JsonDocument stringDocument = JsonDocument.Parse("\"Hello\"");
    private readonly JsonDocument numberDocument = JsonDocument.Parse("1.3");
    private readonly JsonDocument booleanDocument = JsonDocument.Parse("true");
    private readonly JsonDocument nullDocument = JsonDocument.Parse("null");

    /// <summary>
    /// Global cleanup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalCleanup]
    public Task GlobalCleanup()
    {
        this.objectDocument.Dispose();
        this.arrayDocument.Dispose();
        this.stringDocument.Dispose();
        this.numberDocument.Dispose();
        this.booleanDocument.Dispose();
        this.nullDocument.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructAnyFromJsonElement()
    {
        _ = new JsonAny(this.objectDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void AnyFromJsonElement()
    {
        _ = JsonAny.FromJson(this.objectDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructObjectFromJsonElement()
    {
        _ = new JsonObject(this.objectDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ObjectFromJsonElement()
    {
        _ = JsonObject.FromJson(this.objectDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructArrayFromJsonElement()
    {
        _ = new JsonArray(this.arrayDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ArrayFromJsonElement()
    {
        _ = JsonArray.FromJson(this.arrayDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructStringFromJsonElement()
    {
        _ = new JsonString(this.stringDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void StringFromJsonElement()
    {
        _ = JsonString.FromJson(this.stringDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructNumberFromJsonElement()
    {
        _ = new JsonNumber(this.numberDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void NumberFromJsonElement()
    {
        _ = JsonNumber.FromJson(this.numberDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructBooleanFromJsonElement()
    {
        _ = new JsonBoolean(this.booleanDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void BooleanFromJsonElement()
    {
        _ = JsonBoolean.FromJson(this.booleanDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ConstructNullFromJsonElement()
    {
        _ = new JsonNull(this.nullDocument!.RootElement);
    }

    /// <summary>
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void NullFromJsonElement()
    {
        _ = JsonNull.FromJson(this.nullDocument!.RootElement);
    }
}