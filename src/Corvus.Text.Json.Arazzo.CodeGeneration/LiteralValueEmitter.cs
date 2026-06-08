// <copyright file="LiteralValueEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a constant (non-expression) JSON value as a parse-free <see cref="JsonElement"/> local
/// (plan §3.1).
/// </summary>
/// <remarks>
/// <para>
/// A literal parameter value or request-body payload is fixed at generation time, so it is bound once
/// to a <c>static readonly</c> document (process-lifetime, like a cached regex — never disposed) and
/// the local is assigned its root. The document is standalone, so values bound from it via
/// <c>TTarget.From(...)</c> are valid for the whole run.
/// </para>
/// <para>
/// A scalar string or number uses the specialised <c>FixedJsonValueDocument</c>, which simply wraps the
/// raw UTF-8 bytes (no tokenizer, no metadata database) — far cheaper than a full parse. Booleans,
/// nulls, objects, and arrays fall back to <c>ParsedJsonDocument.Parse</c>.
/// </para>
/// </remarks>
internal static class LiteralValueEmitter
{
    /// <summary>
    /// Emits the static field declaration and the in-method statement that assign a
    /// <see cref="JsonElement"/> named <paramref name="resultLocal"/> from a literal JSON value.
    /// </summary>
    /// <param name="fields">Accumulates <c>static readonly</c> field declarations.</param>
    /// <param name="statements">Accumulates the in-method assignment statement.</param>
    /// <param name="literalJson">The literal value's JSON text (e.g. <c>"electronic"</c> or <c>42</c>).</param>
    /// <param name="resultLocal">The name of the <see cref="JsonElement"/> local to assign.</param>
    /// <param name="fieldName">The unique name for the static document field.</param>
    public static void Emit(
        StringBuilder fields,
        StringBuilder statements,
        string literalJson,
        string resultLocal,
        string fieldName)
    {
        char first = literalJson.Length > 0 ? literalJson[0] : '\0';

        // true / false / null have no value beyond their token, so they are shared core singletons —
        // no field, no buffer, no allocation.
        string? singleton = first switch
        {
            't' => "Corvus.Text.Json.Internal.ValuelessJsonDocument<JsonElement>.BooleanTrue",
            'f' => "Corvus.Text.Json.Internal.ValuelessJsonDocument<JsonElement>.BooleanFalse",
            'n' => "Corvus.Text.Json.Internal.ValuelessJsonDocument<JsonElement>.Null",
            _ => null,
        };

        if (singleton is not null)
        {
            statements.Append("JsonElement ").Append(resultLocal).Append(" = ").Append(singleton).AppendLine(".RootElement;");
            return;
        }

        if (first == '"')
        {
            // Scalar string: wrap the raw quoted UTF-8 directly — no parse.
            fields.Append("private static readonly Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement> ").Append(fieldName)
                .Append(" = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForString(")
                .Append(EmitText.Quote(literalJson)).AppendLine("u8.ToArray());");
        }
        else if (first == '-' || (first >= '0' && first <= '9'))
        {
            // Scalar number: wrap the raw UTF-8 number text directly — no parse.
            fields.Append("private static readonly Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement> ").Append(fieldName)
                .Append(" = Corvus.Text.Json.Internal.FixedJsonValueDocument<JsonElement>.ForNumber(")
                .Append(EmitText.Quote(literalJson)).AppendLine("u8.ToArray());");
        }
        else
        {
            // Object or array: parse once into a standalone document.
            fields.Append("private static readonly ParsedJsonDocument<JsonElement> ").Append(fieldName)
                .Append(" = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(")
                .Append(EmitText.Quote(literalJson)).AppendLine("));");
        }

        statements.Append("JsonElement ").Append(resultLocal).Append(" = ").Append(fieldName).AppendLine(".RootElement;");
    }
}