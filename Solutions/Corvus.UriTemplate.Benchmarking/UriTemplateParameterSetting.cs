// <copyright file="UriTemplateParameterSetting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Json.UriTemplates;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class UriTemplateParameterSetting
{
    private const string UriTemplate = "http://example.org/location{?value*}";
    private static readonly JsonAny JsonValues = JsonAny.FromProperties(("foo", "bar"), ("bar", "baz"), ("baz", "bob")).AsJsonElementBackedValue();
    private static readonly Dictionary<string, string> Values = new() { { "foo", "bar" }, { "bar", "baz" }, { "baz", "bob" } };

    private Tavis.UriTemplates.UriTemplate? tavisTemplate;
    private Corvus.UriTemplates.TavisApi.UriTemplate? corvusTavisTemplate;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalSetup]
    public Task GlobalSetup()
    {
        this.tavisTemplate = new(UriTemplate);
        this.corvusTavisTemplate = new(UriTemplate);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalCleanup]
    public Task GlobalCleanup()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolve a URI from a template and parameter values using Tavis.UriTemplate.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ResolveDictionaryTavis()
    {
        this.tavisTemplate!.SetParameter("value", Values);
        this.tavisTemplate!.Resolve();
    }

    /// <summary>
    /// Resolve a URI from a template and parameter values using Corvus.UriTemplates.TavisApi.UriTemplate.
    /// </summary>
    [Benchmark]
    public void ResolveDictionaryCorvusTavis()
    {
        this.corvusTavisTemplate!.SetParameter("value", Values);
        this.corvusTavisTemplate!.Resolve();
    }

    /// <summary>
    /// Resolve a URI from a template and parameter values using Corvus.UriTemplateResolver.
    /// </summary>
    [Benchmark]
    public void ResolveDictionaryCorvus()
    {
        object? nullState = default;
        JsonUriTemplateResolver.TryResolveResult(UriTemplate.AsSpan(), false, JsonValues, HandleResult, ref nullState);
        static void HandleResult(ReadOnlySpan<char> resolvedTemplate, ref object? state)
        {
            // NOP
        }
    }
}