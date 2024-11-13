// <copyright file="ExampleKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The example keyword.
/// </summary>
public sealed class ExampleKeyword : IExamplesProviderKeyword, INonStructuralKeyword
{
    private ExampleKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExampleKeyword"/> keyword.
    /// </summary>
    public static ExampleKeyword Instance { get; } = new ExampleKeyword();

    /// <inheritdoc />
    public string Keyword => "example";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "example"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public bool TryGetExamples(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string[]? example)
    {
        List<string> result = [];

        if (typeDeclaration.TryGetKeyword(this, out JsonElement exampleElement) &&
            exampleElement.ValueKind != JsonValueKind.Undefined)
        {
            result.Add(exampleElement.GetRawText());
        }

        if (result.Count > 0)
        {
            example = [.. result];
            return true;
        }

        example = null;
        return false;
    }
}