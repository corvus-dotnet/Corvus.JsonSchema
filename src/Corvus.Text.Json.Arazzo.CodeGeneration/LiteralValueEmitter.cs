// <copyright file="LiteralValueEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits a constant (non-expression) JSON value as a parsed-once <see cref="JsonElement"/> local
/// (plan §3.1).
/// </summary>
/// <remarks>
/// A literal parameter value or request-body payload is fixed at generation time, so its JSON is
/// parsed once into a <c>static readonly</c> document (process-lifetime, like a cached regex) and the
/// local is assigned its root — no per-execution parse or allocation. The document is standalone, so
/// values bound from it via <c>TTarget.From(...)</c> are valid for the whole run.
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
        fields.Append("private static readonly ParsedJsonDocument<JsonElement> ").Append(fieldName)
            .Append(" = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(")
            .Append(EmitText.Quote(literalJson)).AppendLine("));");
        statements.Append("JsonElement ").Append(resultLocal).Append(" = ").Append(fieldName).AppendLine(".RootElement;");
    }
}