// <copyright file="UriTemplateParameterExtraction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.UriTemplates;

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
    private Corvus.UriTemplates.TavisApi.UriTemplate? corvusTavisTemplate;
    private IUriTemplateParser? corvusTemplate;

    /// <summary>
    /// Global setup.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes once cleanup is complete.</returns>
    [GlobalSetup]
    public Task GlobalSetup()
    {
        this.tavisTemplate = new(UriTemplate);
        this.corvusTavisTemplate = new(UriTemplate);

        // Warm it up.
        this.tavisTemplate!.GetParameters(TavisUri);
        this.corvusTavisTemplate!.GetParameters(TavisUri);

        this.corvusTemplate = UriTemplateParserFactory.CreateParser(UriTemplate);
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
    /// Extract parameters from a URI template using Corvus types.
    /// </summary>
    [Benchmark]
    public void ExtractParametersCorvus()
    {
        object? state = default;
        this.corvusTemplate!.ParseUri(Uri, HandleParameters, ref state);

        static void HandleParameters(bool reset, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ref object? state)
        {
            // NOP
        }
    }

    /// <summary>
    /// Extract parameters from a URI template using Tavis types.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ExtractParametersTavis()
    {
        this.tavisTemplate!.GetParameters(TavisUri);
    }

    /// <summary>
    /// Extract parameters from a URI template using the Corvus implementation of the Tavis API.
    /// </summary>
    [Benchmark]
    public void ExtractParametersCorvusTavis()
    {
        this.corvusTavisTemplate!.GetParameters(TavisUri);
    }
}