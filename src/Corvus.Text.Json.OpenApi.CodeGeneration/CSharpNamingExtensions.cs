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

    /// <summary>
    /// The tags an operation's methods are grouped under: its own tags, or <c>default</c> when it has none.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The tags.</returns>
    public static string[] CSharpClientTags(this in OperationInfo op)
        => op.Tags.Length > 0 ? op.Tags : ["default"];

    /// <summary>
    /// The name of the client for a tag, without the <c>Client</c> suffix: the client name prefix (<c>Api</c> by
    /// default) followed by the sanitised tag. The interface is <c>I{name}Client</c> and the class <c>{name}Client</c>.
    /// </summary>
    /// <param name="clientNamePrefix">The configured client name prefix, or <see langword="null"/> for the default.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The client name.</returns>
    public static string CSharpClientName(string? clientNamePrefix, string tag)
        => $"{clientNamePrefix ?? "Api"}{CodeEmitHelpers.SanitizeIdentifier(tag)}";
}