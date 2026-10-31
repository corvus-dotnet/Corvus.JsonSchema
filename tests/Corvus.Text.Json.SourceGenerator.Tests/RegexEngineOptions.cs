// <copyright file="RegexEngineOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.SourceGenerator.Tests;

/// <summary>
/// The build properties a generator driver gives the source generator, for the tests that choose the engine its
/// regular expressions are written for (<c>CorvusTextJsonRegexEngine</c>, or <c>CorvusJsonSchemaRegexEngine</c> for the
/// V4 source generator).
/// </summary>
internal static class RegexEngineOptions
{
    /// <summary>Gets the options of a build that sets <c>CorvusTextJsonRegexEngine</c>.</summary>
    /// <param name="engine">The engine: <c>Ecma262</c> or <c>DotNet</c>.</param>
    /// <returns>The options.</returns>
    public static AnalyzerConfigOptionsProvider For(string engine) =>
        new GlobalOptionsProvider(ImmutableDictionary<string, string>.Empty.Add("build_property.CorvusTextJsonRegexEngine", engine));

    /// <summary>Gets the options of a build that sets the V4 source generator's <c>CorvusJsonSchemaRegexEngine</c>.</summary>
    /// <param name="engine">The engine: <c>Ecma262</c> or <c>DotNet</c>.</param>
    /// <returns>The options.</returns>
    public static AnalyzerConfigOptionsProvider ForV4(string engine) =>
        new GlobalOptionsProvider(ImmutableDictionary<string, string>.Empty.Add("build_property.CorvusJsonSchemaRegexEngine", engine));

    private sealed class GlobalOptionsProvider(ImmutableDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions None = new DictionaryOptions(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GlobalOptions { get; } = new DictionaryOptions(values);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => None;
    }

    private sealed class DictionaryOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}