// <copyright file="WellKnownStringFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Handlers for well-known string formats.
/// </summary>
public class WellKnownStringFormatHandler : IStringFormatHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="WellKnownStringFormatHandler"/>.
    /// </summary>
    public static WellKnownStringFormatHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint Priority => 100_000;

    /// <inheritdoc/>
    public bool AppendFormatAssertion(CodeGenerator generator, string format, string formatKeywordProviderExpression, string valueIdentifier, string validationContextIdentifier)
    {
        switch (format)
        {
            case "date":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchDate(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "date-time":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchDateTime(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "time":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchTime(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "duration":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchDuration(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "email":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchEmail(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "idn-email":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIdnEmail(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "hostname":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchHostname(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "idn-hostname":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIdnHostname(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "ipv4":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIPV4(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "ipv6":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIPV6(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "uuid":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchUuid(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "uri":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchUri(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "uri-template":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchUriTemplate(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "uri-reference":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchUriReference(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "iri":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIri(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "iri-reference":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchIriReference(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "json-pointer":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchJsonPointer(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "relative-json-pointer":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchRelativeJsonPointer(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "regex":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchRegex(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "corvus-base64-content":
                return false;

            case "corvus-base64-content-pre201909":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchBase64Content(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "corvus-base64-string":
                return false;

            case "corvus-base64-string-pre201909":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchBase64String(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            case "corvus-json-content":
                return false;

            case "corvus-json-content-pre201909":
                generator.AppendIndent(
                   "JsonSchemaEvaluation.MatchJsonContent(",
                   valueIdentifier, ", ",
                   formatKeywordProviderExpression, ", ",
                   "ref ", validationContextIdentifier, ")");
                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators, bool forMutable, bool useExplicit = false)
    {
        string typeName = forMutable ? "Mutable" : typeDeclaration.DotnetTypeName();
        string operatorKind = useExplicit ? "explicit" : "implicit";

        switch (format)
        {
            case "date":
                if (seenConversionOperators.Add("LocalDate"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator NodaTime.LocalDate(", typeName, " value) => value._parent.TryGetValue(value._idx, out NodaTime.LocalDate result) ? result : throw new FormatException();");
                }

                return true;

            case "date-time":
                if (seenConversionOperators.Add("OffsetDateTime"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator NodaTime.OffsetDateTime(", typeName, " value) => value._parent.TryGetValue(value._idx, out NodaTime.OffsetDateTime result) ? result : throw new FormatException();");
                }

                return true;

            case "time":
                if (seenConversionOperators.Add("OffsetTime"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator NodaTime.OffsetTime(", typeName, " value) => value._parent.TryGetValue(value._idx, out NodaTime.OffsetTime result) ? result : throw new FormatException();");
                }

                return true;

            case "duration":
                if (seenConversionOperators.Add("Period"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator Period(", typeName, " value) => value._parent.TryGetValue(value._idx, out Period result) ? result : throw new FormatException();");
                }

                return true;

            case "ipv4":
                return false;

            case "ipv6":
                return false;

            case "uuid":
                if (seenConversionOperators.Add("Guid"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator Guid(", typeName, " value) => value._parent.TryGetValue(value._idx, out Guid result) ? result : throw new FormatException();");
                }

                return true;

            case "uri":
                if (seenConversionOperators.Add("Utf8UriValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator Utf8UriValue(", typeName, " value) => Utf8UriValue.TryGetValue(value._parent, value._idx, out Utf8UriValue result) ? result : throw new FormatException();");
                }

                return true;

            case "uri-reference":
                if (seenConversionOperators.Add("Utf8UriReferenceValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator Utf8UriReferenceValue(", typeName, " value) => Utf8UriReferenceValue.TryGetValue(value._parent, value._idx, out Utf8UriReferenceValue result) ? result : throw new FormatException();");
                }

                return true;

            case "iri":
                if (seenConversionOperators.Add("Utf8IriValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator Utf8IriValue(", typeName, " value) => Utf8IriValue.TryGetValue(value._parent, value._idx, out Utf8IriValue result) ? result : throw new FormatException();");
                }

                return true;

            case "iri-reference":
                if (seenConversionOperators.Add("Utf8IriReferenceValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator Utf8IriReferenceValue(", typeName, " value) => Utf8IriReferenceValue.TryGetValue(value._parent, value._idx, out Utf8IriReferenceValue result) ? result : throw new FormatException();");
                }

                return true;

            case "regex":
                return false;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public bool AppendFormatValueGetters(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators)
    {
        switch (format)
        {
            case "date":
                if (seenConversionOperators.Add("LocalDate"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out NodaTime.LocalDate value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "date-time":
                if (seenConversionOperators.Add("OffsetDateTime"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out NodaTime.OffsetDateTime value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "time":
                if (seenConversionOperators.Add("OffsetTime"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out NodaTime.OffsetTime value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "duration":
                if (seenConversionOperators.Add("Period"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Period value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "ipv4":
                return false;

            case "ipv6":
                return false;

            case "uuid":
                if (seenConversionOperators.Add("Guid"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Guid value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "uri":
                if (seenConversionOperators.Add("Utf8UriValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Utf8UriValue value) { CheckValidInstance(); return Utf8UriValue.TryGetValue(_parent, _idx, out value); }");
                }

                return true;

            case "uri-reference":
                if (seenConversionOperators.Add("Utf8UriReferenceValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Utf8UriReferenceValue value) { CheckValidInstance(); return Utf8UriReferenceValue.TryGetValue(_parent, _idx, out value); }");
                }

                return true;

            case "iri":
                if (seenConversionOperators.Add("Utf8IriValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Utf8IriValue value) { CheckValidInstance(); return Utf8IriValue.TryGetValue(_parent, _idx, out value); }");
                }

                return true;

            case "iri-reference":
                if (seenConversionOperators.Add("Utf8IriReferenceValue"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Utf8IriReferenceValue value) { CheckValidInstance(); return Utf8IriReferenceValue.TryGetValue(_parent, _idx, out value); }");
                }

                return true;

            case "regex":
                return false;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public bool AppendFormatSourceConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConstructorParameters)
    {
        switch (format)
        {
            case "date":
                if (seenConstructorParameters.Add("LocalDate"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(NodaTime.LocalDate value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatLocalDate(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                return true;

            case "date-time":
                if (seenConstructorParameters.Add("OffsetDateTime"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(NodaTime.OffsetDateTime value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatOffsetDateTime(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                if (seenConstructorParameters.Add("DateTimeOffset"))
                {
                    generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private Source(DateTimeOffset value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                return true;

            case "time":
                if (seenConstructorParameters.Add("OffsetTime"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(NodaTime.OffsetTime value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatOffsetTime(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                return true;

            case "duration":
                if (seenConstructorParameters.Add("Period"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(Period value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatPeriod(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                return true;

            case "ipv4":
                return true;

            case "ipv6":
                return true;

            case "uuid":
                if (seenConstructorParameters.Add("Guid"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(Guid value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.StringSimpleType; }");
                }

                return true;

            case "uri":
                if (seenConstructorParameters.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(Uri value) { _utf16Backing = value.OriginalString.AsSpan(); _kind = Kind.Utf16String; }");
                }

                return true;

            case "uri-reference":
                if (seenConstructorParameters.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(Uri value) { _utf16Backing = value.OriginalString.AsSpan(); _kind = Kind.Utf16String; }");
                }

                return true;

            case "iri":
                if (seenConstructorParameters.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(Uri value) { _utf16Backing = value.OriginalString.AsSpan(); _kind = Kind.Utf16String; }");
                }

                return true;

            case "iri-reference":
                if (seenConstructorParameters.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                       .AppendLineIndent("private Source(Uri value) { _utf16Backing = value.OriginalString.AsSpan(); _kind = Kind.Utf16String; }");
                }

                return true;

            case "regex":
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Appends format-specific source conversion operators to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <param name="seenConversionOperators">The set of conversion operators that have already been generated.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public bool AppendFormatSourceConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators)
    {
        switch (format)
        {
            case "date":
                if (seenConversionOperators.Add("LocalDate"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(NodaTime.LocalDate value) => new (value);");
                }

                return true;

            case "date-time":
                if (seenConversionOperators.Add("OffsetDateTime"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(NodaTime.OffsetDateTime value) => new (value);");
                }

                return true;

            case "time":
                if (seenConversionOperators.Add("OffsetTime"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(NodaTime.OffsetTime value) => new (value);");
                }

                return true;

            case "duration":
                if (seenConversionOperators.Add("Period"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(Period value) => new (value);");
                }

                return true;

            case "ipv4":
                return true;

            case "ipv6":
                return true;

            case "uuid":
                if (seenConversionOperators.Add("Guid"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(Guid value) => new (value);");
                }

                return true;

            case "uri":
                if (seenConversionOperators.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(Uri value) => new (value);");
                }

                return true;

            case "uri-reference":
                if (seenConversionOperators.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(Uri value) => new (value);");
                }

                return true;

            case "iri":
                if (seenConversionOperators.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(Uri value) => new (value);");
                }

                return true;

            case "iri-reference":
                if (seenConversionOperators.Add("Uri"))
                {
                    generator
                        .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator Source(Uri value) => new (value);");
                }

                return true;

            case "regex":
                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public JsonTokenType? GetExpectedTokenType(string format)
    {
        if (HandlesFormat(format))
        {
            return JsonTokenType.String;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the specified format requires simple types backing.
    /// </summary>
    /// <param name="format">The format to check.</param>
    /// <param name="requiresSimpleType">When this method returns, contains <see langword="true"/> if the format requires simple types backing; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if this handler supports the specified format; otherwise, <see langword="false"/>.</returns>
    public bool RequiresSimpleTypesBacking(string format, out bool requiresSimpleType)
    {
        switch (format)
        {
            case "date":
                requiresSimpleType = true;
                return true;

            case "date-time":
                requiresSimpleType = true;
                return true;

            case "time":
                requiresSimpleType = true;
                return true;

            case "duration":
                requiresSimpleType = true;
                return true;

            case "ipv4":
                requiresSimpleType = false;
                return true;

            case "ipv6":
                requiresSimpleType = false;
                return true;

            case "uuid":
                requiresSimpleType = true;
                return true;

            case "uri":
                requiresSimpleType = false;
                return true;

            case "uri-reference":
                requiresSimpleType = false;
                return true;

            case "iri":
                requiresSimpleType = false;
                return true;

            case "iri-reference":
                requiresSimpleType = false;
                return true;

            case "regex":
                requiresSimpleType = false;
                return true;

            default:
                requiresSimpleType = false;
                return false;
        }
    }

    public bool AppendFormatToStringAndTryFormatOverrides(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, bool forMutable)
    {
        switch (format)
        {
            case "date":
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateOnly.ToString(string?, IFormatProvider?)"/>, which accepts
                        /// standard .NET date format strings (e.g. <c>"d"</c>, <c>"D"</c>, <c>"o"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <c>NodaTime.LocalDate.ToString</c>, which uses NodaTime pattern syntax
                        /// (e.g. <c>"uuuu-MM-dd"</c>, <c>"d MMMM uuuu"</c>). The standard .NET round-trip
                        /// format specifiers <c>"o"</c> and <c>"O"</c> are automatically translated to the
                        /// equivalent NodaTime pattern <c>"uuuu-MM-dd"</c>.
                        /// </para>
                        /// <para>
                        /// When <paramref name="format"/> is <see langword="null"/> or empty, the canonical
                        /// JSON string representation is returned on all target frameworks.
                        /// </para>
                        /// </remarks>
                        public string ToString(string? format, IFormatProvider? formatProvider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!string.IsNullOrEmpty(format) && _parent.TryGetValue(_idx, out global::System.DateOnly value))
                                return value.ToString(format, formatProvider);
                        #else
                            if (!string.IsNullOrEmpty(format) && TryGetValue(out NodaTime.LocalDate value))
                            {
                                if (format is "o" or "O")
                                    return value.ToString("uuuu-MM-dd", formatProvider);
                                return value.ToString(format, formatProvider);
                            }
                        #endif
                            return _parent.ToString(_idx, format, formatProvider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateOnly.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET date format strings (e.g. <c>"d"</c>, <c>"D"</c>, <c>"o"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.DateOnly value))
                                return value.TryFormat(destination, out charsWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateOnly.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET date format strings and writes UTF-8 encoded output.
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written as UTF-8 instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.DateOnly value))
                                return value.TryFormat(utf8Destination, out bytesWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
                        }
                        """);
                return true;

            case "date-time":
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateTimeOffset.ToString(string?, IFormatProvider?)"/>, which accepts
                        /// standard .NET date-time format strings (e.g. <c>"G"</c>, <c>"o"</c>, <c>"r"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <c>NodaTime.OffsetDateTime.ToString</c>, which uses NodaTime pattern syntax
                        /// (e.g. <c>"uuuu'/'MM'/'dd HH:mm:ss"</c>). The standard .NET round-trip
                        /// format specifiers <c>"o"</c> and <c>"O"</c> are automatically translated to the
                        /// equivalent NodaTime pattern.
                        /// </para>
                        /// <para>
                        /// When <paramref name="format"/> is <see langword="null"/> or empty, the canonical
                        /// JSON string representation is returned on all target frameworks.
                        /// </para>
                        /// </remarks>
                        public string ToString(string? format, IFormatProvider? formatProvider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!string.IsNullOrEmpty(format) && _parent.TryGetValue(_idx, out global::System.DateTimeOffset value))
                                return value.ToString(format, formatProvider);
                        #else
                            if (!string.IsNullOrEmpty(format) && TryGetValue(out NodaTime.OffsetDateTime value))
                            {
                                if (format is "o" or "O")
                                    return value.ToString("uuuu-MM-ddTHH:mm:ss.fffffffo<+HH:mm>", formatProvider);
                                return value.ToString(format, formatProvider);
                            }
                        #endif
                            return _parent.ToString(_idx, format, formatProvider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateTimeOffset.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET date-time format strings (e.g. <c>"G"</c>, <c>"o"</c>, <c>"r"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.DateTimeOffset value))
                                return value.TryFormat(destination, out charsWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.DateTimeOffset.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET date-time format strings and writes UTF-8 encoded output.
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written as UTF-8 instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.DateTimeOffset value))
                                return value.TryFormat(utf8Destination, out bytesWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
                        }
                        """);
                return true;

            case "time":
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.TimeOnly.ToString(string?, IFormatProvider?)"/>, which accepts
                        /// standard .NET time format strings (e.g. <c>"t"</c>, <c>"T"</c>, <c>"o"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <c>NodaTime.OffsetTime.ToString</c>, which uses NodaTime pattern syntax
                        /// (e.g. <c>"HH:mm:ss"</c>, <c>"HH'.'mm'.'ss"</c>). The standard .NET round-trip
                        /// format specifiers <c>"o"</c> and <c>"O"</c> are automatically translated to the
                        /// equivalent NodaTime pattern.
                        /// </para>
                        /// <para>
                        /// When <paramref name="format"/> is <see langword="null"/> or empty, the canonical
                        /// JSON string representation is returned on all target frameworks.
                        /// </para>
                        /// </remarks>
                        public string ToString(string? format, IFormatProvider? formatProvider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!string.IsNullOrEmpty(format) && _parent.TryGetValue(_idx, out global::System.TimeOnly value))
                                return value.ToString(format, formatProvider);
                        #else
                            if (!string.IsNullOrEmpty(format) && TryGetValue(out NodaTime.OffsetTime value))
                            {
                                if (format is "o" or "O")
                                    return value.ToString("HH:mm:ss.fffffff", formatProvider);
                                return value.ToString(format, formatProvider);
                            }
                        #endif
                            return _parent.ToString(_idx, format, formatProvider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.TimeOnly.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET time format strings (e.g. <c>"t"</c>, <c>"T"</c>, <c>"o"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.TimeOnly value))
                                return value.TryFormat(destination, out charsWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.TimeOnly.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/>,
                        /// which accepts standard .NET time format strings and writes UTF-8 encoded output.
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written as UTF-8 instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && _parent.TryGetValue(_idx, out global::System.TimeOnly value))
                                return value.TryFormat(utf8Destination, out bytesWritten, format, provider);
                        #endif
                            return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
                        }
                        """);
                return true;

            case "duration":
                return false;

            case "uuid":
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// When a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.Guid.ToString(string?, IFormatProvider?)"/>, which accepts
                        /// standard .NET GUID format strings (<c>"D"</c>, <c>"N"</c>, <c>"B"</c>, <c>"P"</c>, <c>"X"</c>).
                        /// This behaviour is identical on all target frameworks.
                        /// </para>
                        /// <para>
                        /// When <paramref name="format"/> is <see langword="null"/> or empty, the canonical
                        /// JSON string representation is returned.
                        /// </para>
                        /// </remarks>
                        public string ToString(string? format, IFormatProvider? formatProvider)
                        {
                            CheckValidInstance();
                            if (!string.IsNullOrEmpty(format) && TryGetValue(out Guid value))
                                return value.ToString(format, formatProvider);
                            return _parent.ToString(_idx, format, formatProvider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.Guid.TryFormat(Span{char}, out int, ReadOnlySpan{char})"/>,
                        /// which accepts standard .NET GUID format strings (<c>"D"</c>, <c>"N"</c>, <c>"B"</c>, <c>"P"</c>, <c>"X"</c>).
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && TryGetValue(out Guid value))
                                return value.TryFormat(destination, out charsWritten, format);
                        #endif
                            return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
                        }
                        """)
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        """
                        /// <inheritdoc/>
                        /// <remarks>
                        /// <para>
                        /// On .NET, when a non-empty <paramref name="format"/> is provided, this delegates to
                        /// <see cref="System.Guid.TryFormat(Span{byte}, out int, ReadOnlySpan{char})"/>,
                        /// which accepts standard .NET GUID format strings and writes UTF-8 encoded output.
                        /// </para>
                        /// <para>
                        /// On netstandard2.0, or when <paramref name="format"/> is empty, the canonical
                        /// JSON string representation is written as UTF-8 instead.
                        /// </para>
                        /// </remarks>
                        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                        {
                            CheckValidInstance();
                        #if NET
                            if (!format.IsEmpty && TryGetValue(out Guid value))
                                return value.TryFormat(utf8Destination, out bytesWritten, format);
                        #endif
                            return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
                        }
                        """);
                return true;

            case "uri":
                return AppendUriLikeFormatOverrides(generator, "Utf8UriValue", "Utf8Uri", "Uri", "URI", "Uri");

            case "uri-reference":
                return AppendUriLikeFormatOverrides(generator, "Utf8UriReferenceValue", "Utf8UriReference", "UriReference", "URI reference", "UriReference");

            case "iri":
                return AppendUriLikeFormatOverrides(generator, "Utf8IriValue", "Utf8Iri", "Iri", "IRI", "Iri");

            case "iri-reference":
                return AppendUriLikeFormatOverrides(generator, "Utf8IriReferenceValue", "Utf8IriReference", "IriReference", "IRI reference", "IriReference");

            default:
                return false;
        }
    }

    private static bool AppendUriLikeFormatOverrides(
        CodeGenerator generator,
        string valueTypeName,
        string uriTypeName,
        string uriPropertyName,
        string formatKind,
        string helperMethodSuffix)
    {
        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc/>
                /// <remarks>
                /// <para>
                /// When <paramref name="format"/> is <c>"g"</c> or <c>"G"</c>, produces the display form of
                /// the {{formatKind}} with percent-encoded sequences decoded for human readability.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is <c>"c"</c> or <c>"C"</c>, produces the canonical form of
                /// the {{formatKind}} with all required characters percent-encoded.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is <see langword="null"/> or empty, or an unrecognised
                /// format string is provided, the raw JSON string representation is returned.
                /// </para>
                /// <para>
                /// There are no culture-specific variations; <paramref name="formatProvider"/> is ignored.
                /// </para>
                /// </remarks>
                public string ToString(string? format, IFormatProvider? formatProvider)
                {
                    CheckValidInstance();
                    if (!string.IsNullOrEmpty(format) && (format is "g" or "G" or "c" or "C") && TryGetValue(out {{valueTypeName}} uriValue))
                    {
                        using (uriValue)
                        {
                            bool isDisplay = format is "g" or "G";
                            {{uriTypeName}} uri = uriValue.{{uriPropertyName}};
                            if (global::Corvus.Text.Json.Internal.JsonElementHelpers.TryFormat{{helperMethodSuffix}}(uri, isDisplay, out string? result))
                                return result;
                        }
                    }
                    return _parent.ToString(_idx, format, formatProvider);
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc/>
                /// <remarks>
                /// <para>
                /// When <paramref name="format"/> is <c>"g"</c> or <c>"G"</c>, writes the display form of
                /// the {{formatKind}} with percent-encoded sequences decoded for human readability.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is <c>"c"</c> or <c>"C"</c>, writes the canonical form of
                /// the {{formatKind}} with all required characters percent-encoded.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is empty, or an unrecognised format string is provided,
                /// the raw JSON string representation is written.
                /// </para>
                /// <para>
                /// There are no culture-specific variations; <paramref name="provider"/> is ignored.
                /// </para>
                /// </remarks>
                public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                {
                    CheckValidInstance();
                    if (!format.IsEmpty && format.Length == 1)
                    {
                        char f = format[0];
                        bool isDisplay = f == 'g' || f == 'G';
                        bool isCanonical = !isDisplay && (f == 'c' || f == 'C');
                        if ((isDisplay || isCanonical) && TryGetValue(out {{valueTypeName}} uriValue))
                        {
                            using (uriValue)
                            {
                                {{uriTypeName}} uri = uriValue.{{uriPropertyName}};
                                return global::Corvus.Text.Json.Internal.JsonElementHelpers.TryFormat{{helperMethodSuffix}}(uri, isDisplay, destination, out charsWritten);
                            }
                        }
                    }
                    return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc/>
                /// <remarks>
                /// <para>
                /// When <paramref name="format"/> is <c>"g"</c> or <c>"G"</c>, writes the display form of
                /// the {{formatKind}} as UTF-8 with percent-encoded sequences decoded for human readability.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is <c>"c"</c> or <c>"C"</c>, writes the canonical form of
                /// the {{formatKind}} as UTF-8 with all required characters percent-encoded.
                /// </para>
                /// <para>
                /// When <paramref name="format"/> is empty, or an unrecognised format string is provided,
                /// the raw JSON string representation is written as UTF-8.
                /// </para>
                /// <para>
                /// There are no culture-specific variations; <paramref name="provider"/> is ignored.
                /// </para>
                /// </remarks>
                public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                {
                    CheckValidInstance();
                    if (!format.IsEmpty && format.Length == 1)
                    {
                        char f = format[0];
                        bool isDisplay = f == 'g' || f == 'G';
                        bool isCanonical = !isDisplay && (f == 'c' || f == 'C');
                        if ((isDisplay || isCanonical) && TryGetValue(out {{valueTypeName}} uriValue))
                        {
                            using (uriValue)
                            {
                                {{uriTypeName}} uri = uriValue.{{uriPropertyName}};
                                if (isDisplay)
                                {
                                    if (uri.TryFormatDisplay(utf8Destination, out bytesWritten))
                                        return true;
                                }
                                else
                                {
                                    if (uri.TryFormatCanonical(utf8Destination, out bytesWritten))
                                        return true;
                                }
                                bytesWritten = 0;
                                return false;
                            }
                        }
                    }
                    return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
                }
                """);
        return true;
    }

    private static bool HandlesFormat(string format)
    {
        return format switch
        {
            "date" => true,
            "date-time" => true,
            "time" => true,
            "duration" => true,
            "email" => true,
            "idn-email" => true,
            "hostname" => true,
            "idn-hostname" => true,
            "ipv4" => true,
            "ipv6" => true,
            "uuid" => true,
            "uri" => true,
            "uri-template" => true,
            "uri-reference" => true,
            "iri" => true,
            "iri-reference" => true,
            "json-pointer" => true,
            "relative-json-pointer" => true,
            "regex" => true,
            "corvus-base64-content" => true,
            "corvus-base64-content-pre201909" => true,
            "corvus-base64-string" => true,
            "corvus-base64-string-pre201909" => true,
            "corvus-json-content" => true,
            "corvus-json-content-pre201909" => true,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool TryGetSimpleTypeNameSuffix(string format, [NotNullWhen(true)] out string? suffix)
    {
        suffix = format switch
        {
            "date" => "Date",
            "date-time" => "DateTime",
            "time" => "Time",
            "duration" => "Duration",
            "email" => "Email",
            "idn-email" => "IdnEmail",
            "hostname" => "Hostname",
            "idn-hostname" => "IdnHostname",
            "ipv4" => "IpV4",
            "ipv6" => "IpV6",
            "uuid" => "Uuid",
            "uri" => "Uri",
            "uri-template" => "UriTemplate",
            "uri-reference" => "UriReference",
            "iri" => "Iri",
            "iri-reference" => "IriReference",
            "json-pointer" => "Pointer",
            "relative-json-pointer" => "RelativePointer",
            "regex" => "Regex",
            _ => null,
        };

        return suffix is not null;
    }
}