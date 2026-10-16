// <copyright file="CSharpNamingExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The C# names the C# emitters derive from the language-neutral operation model.
/// </summary>
public static class CSharpNamingExtensions
{
    /// <summary>
    /// The generated C# method name of an operation (PascalCase, without the <c>Async</c> suffix): the operationId
    /// when there is one, else the HTTP method and the path.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The method name.</returns>
    public static string CSharpMethodName(this in OperationInfo op)
        => CodeEmitHelpers.GetMethodName(op.OperationId, op.Method, op.PathTemplate, op.CustomMethodName);

    /// <summary>
    /// The generated C# parameter name of a binary multipart part.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>The parameter name.</returns>
    public static string CSharpParameterName(this in BinaryPropertyInfo part)
        => CodeEmitHelpers.SanitizeParameterName(part.PropertyName);
}