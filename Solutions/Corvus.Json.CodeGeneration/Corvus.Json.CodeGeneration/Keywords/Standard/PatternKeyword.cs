// <copyright file="PatternKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The pattern keyword.
/// </summary>
public sealed class PatternKeyword : IStringRegexValidationProviderKeyword
{
    private PatternKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="PatternKeyword"/> keyword.
    /// </summary>
    public static PatternKeyword Instance { get; } = new PatternKeyword();

    /// <inheritdoc />
    public string Keyword => "pattern";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "pattern"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.String
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresStringLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool TryGetValidationRegularExpressions(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out IReadOnlyList<string>? regexes)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement regex) &&
            regex.ValueKind == JsonValueKind.String &&
            regex.GetString() is string regexValue)
        {
            regexes = [regexValue];
            return true;
        }

        regexes = null;
        return false;
    }
}