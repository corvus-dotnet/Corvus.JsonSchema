// <copyright file="OperationTypeNameMapper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Qualifies a resolved operation's generated client type names with the namespace its client was
/// generated into, producing the fully-qualified request/response type names the emitted Arazzo
/// executor references (plan §3.1).
/// </summary>
/// <remarks>
/// The simple type names are taken verbatim from the OpenAPI generator (via
/// <see cref="ResolvedOperation"/>); this type only prepends the namespace, so the Arazzo generator
/// applies no naming heuristic of its own.
/// </remarks>
public static class OperationTypeNameMapper
{
    /// <summary>
    /// Maps a resolved operation to its fully-qualified generated request/response type names.
    /// </summary>
    /// <param name="operation">The resolved operation.</param>
    /// <param name="clientNamespace">The root namespace the operation's client was generated into.</param>
    /// <returns>The generated type names.</returns>
    public static GeneratedOperationTypes Map(in ResolvedOperation operation, string clientNamespace)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientNamespace);
        return new GeneratedOperationTypes(
            operation.MethodName,
            $"{clientNamespace}.{operation.RequestTypeName}",
            $"{clientNamespace}.{operation.ResponseTypeName}");
    }
}