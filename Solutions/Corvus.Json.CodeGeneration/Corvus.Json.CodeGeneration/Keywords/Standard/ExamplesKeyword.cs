// <copyright file="ExamplesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The examples keyword.
/// </summary>
public sealed class ExamplesKeyword : IExamplesProviderKeyword
{
    private ExamplesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ExamplesKeyword"/> keyword.
    /// </summary>
    public static ExamplesKeyword Instance { get; } = new ExamplesKeyword();

    /// <inheritdoc />
    public string Keyword => "examples";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "examples"u8;

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => true;

    /// <inheritdoc/>
    public bool TryGetExamples(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string[]? examples)
    {
        List<string> result = [];

        if (typeDeclaration.TryGetKeyword(this, out JsonElement examplesElement) &&
            examplesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement example in examplesElement.EnumerateArray())
            {
                if (example.ValueKind == JsonValueKind.String)
                {
                    string? value = example.GetString();
                    if (value is string exampleValue)
                    {
                        result.Add(exampleValue);
                    }
                }
            }
        }

        if (result.Count > 0)
        {
            examples = [.. result];
            return true;
        }

        examples = null;
        return false;
    }
}