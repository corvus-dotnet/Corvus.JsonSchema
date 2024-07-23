// <copyright file="FormatWithAnnotationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The format keyword.
/// </summary>
public sealed class FormatWithAnnotationKeyword : IFormatProviderKeyword, IValueKindValidationKeyword
{
    private FormatWithAnnotationKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="FormatWithAnnotationKeyword "/> keyword.
    /// </summary>
    public static FormatWithAnnotationKeyword Instance { get; } = new FormatWithAnnotationKeyword();

    /// <inheritdoc />
    public string Keyword => "format";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "format"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement formatValue)
            ? Format.GetCoreTypesFor(formatValue)
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetFormat(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out string? format)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement formatValue))
        {
            format = formatValue.GetString();
        }
        else
        {
            format = null;
        }

        return format != null;
    }
}