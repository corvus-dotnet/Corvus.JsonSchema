// <copyright file="OperationTypeNameMapper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Maps a resolved operation to the generated client request/response type names, mirroring the
/// OpenAPI generator's own naming so the emitted Arazzo executor references the exact types the
/// client generator produces (plan §3.1).
/// </summary>
/// <remarks>
/// The naming rule is intentionally a faithful copy of the OpenAPI generators' <c>GetMethodName</c>:
/// the method name is the PascalCased <c>operationId</c> when present, otherwise it is derived from
/// the HTTP method and path template; the request and response structs are <c>{MethodName}Request</c>
/// and <c>{MethodName}Response</c> in the client's root namespace.
/// </remarks>
public static class OperationTypeNameMapper
{
    /// <summary>
    /// Maps a resolved operation to its generated request/response type names.
    /// </summary>
    /// <param name="operation">The resolved operation.</param>
    /// <param name="clientNamespace">The root namespace the operation's client was generated into.</param>
    /// <returns>The generated type names.</returns>
    public static GeneratedOperationTypes Map(in ResolvedOperation operation, string clientNamespace)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientNamespace);
        string methodName = GetMethodName(operation.OperationId, operation.Method, operation.Path);
        return new GeneratedOperationTypes(
            methodName,
            $"{clientNamespace}.{methodName}Request",
            $"{clientNamespace}.{methodName}Response");
    }

    private static string GetMethodName(string? operationId, OperationMethod method, string pathTemplate)
    {
        if (operationId is not null)
        {
            return CodeEmitHelpers.ToPascalCase(operationId);
        }

        string methodPrefix = method switch
        {
            OperationMethod.Get => "get",
            OperationMethod.Put => "put",
            OperationMethod.Post => "post",
            OperationMethod.Delete => "delete",
            OperationMethod.Options => "options",
            OperationMethod.Head => "head",
            OperationMethod.Patch => "patch",
            OperationMethod.Trace => "trace",
            _ => throw new UnreachableException(),
        };

        string pathPart = pathTemplate
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        return CodeEmitHelpers.ToPascalCase(methodPrefix + " " + pathPart);
    }
}