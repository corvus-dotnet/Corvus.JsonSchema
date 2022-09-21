// <copyright file="UriTemplateParameterExtraction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Corvus.Json.UriTemplates;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class UriTemplateParameterExtraction
{
    private const string Uri = "http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId=123232323&hash=23ADE34FAE&callback=http%3A%2F%2Fexample.com%2Fcallback";
    private const string UriTemplate = "http://example.com/Glimpse.axd?n=glimpse_ajax&parentRequestId={parentRequestId}{&hash,callback}";
    private static readonly Uri TavisUri = new(Uri);
    private Tavis.UriTemplates.UriTemplate? tavisTemplate;
    private UriTemplateParser.IUriParser? corvusTemplate;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalSetup]
    public Task GlobalSetup()
    {
        this.tavisTemplate = new(UriTemplate);
        this.corvusTemplate = UriTemplateParser.CreateParser(UriTemplate);
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
    /// Validates using the Corvus types.
    /// </summary>
    [Benchmark]
    public void ExtractParametersCorvus()
    {
        this.corvusTemplate!.ParseUri(Uri, HandleParameters);

        static void HandleParameters(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value)
        {
            // NOP
        }
    }

    /// <summary>
    /// Validates using the JsonEverything types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ExtractParametersTavis()
    {
        this.tavisTemplate!.GetParameters(TavisUri);
    }
}