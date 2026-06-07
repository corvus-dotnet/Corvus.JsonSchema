// <copyright file="OperationResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Resolves the operations a workflow step targets against a single source description, mapping a
/// step's <c>operationId</c> or <c>operationPath</c> to the generator's
/// <see cref="OperationDescriptor"/> for it (plan §3.1).
/// </summary>
/// <remarks>
/// The resolver is built from the OpenAPI generator's own <see cref="OperationDescriptor"/> list
/// (produced by <c>DescribeOperations</c>), so it depends on nothing but those descriptors and never
/// re-derives the client's naming or type convention.
/// </remarks>
public sealed class OperationResolver
{
    private readonly string sourceName;
    private readonly Dictionary<string, OperationDescriptor> byOperationId;
    private readonly Dictionary<PathMethod, OperationDescriptor> byPathMethod;

    private OperationResolver(string sourceName, IReadOnlyList<OperationDescriptor> operations)
    {
        this.sourceName = sourceName;
        this.byOperationId = new Dictionary<string, OperationDescriptor>(StringComparer.Ordinal);
        this.byPathMethod = new Dictionary<PathMethod, OperationDescriptor>();
        foreach (OperationDescriptor operation in operations)
        {
            if (operation.OperationId is { } id)
            {
                // Arazzo requires operationId to be unique across the referenced description; the
                // first declaration wins, matching how clients resolve duplicates.
                this.byOperationId.TryAdd(id, operation);
            }

            this.byPathMethod.TryAdd(new PathMethod(operation.Path, operation.Method), operation);
        }
    }

    /// <summary>
    /// Creates a resolver for a source description from the generator's operation descriptors.
    /// </summary>
    /// <param name="sourceName">The <c>name</c> of the source description.</param>
    /// <param name="operations">
    /// The descriptors for the source's client, as returned by the OpenAPI generator's
    /// <c>DescribeOperations</c>.
    /// </param>
    /// <returns>The resolver.</returns>
    public static OperationResolver Create(string sourceName, IReadOnlyList<OperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(operations);
        return new OperationResolver(sourceName, operations);
    }

    /// <summary>
    /// Resolves a step's <c>operationId</c> to its operation.
    /// </summary>
    /// <param name="operationId">The <c>operationId</c>.</param>
    /// <param name="operation">When this method returns <see langword="true"/>, the resolved operation.</param>
    /// <returns><see langword="true"/> if an operation with the id exists; otherwise <see langword="false"/>.</returns>
    public bool TryResolveOperationId(string operationId, out ResolvedOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        if (this.byOperationId.TryGetValue(operationId, out OperationDescriptor descriptor))
        {
            operation = new ResolvedOperation(this.sourceName, descriptor);
            return true;
        }

        operation = default;
        return false;
    }

    /// <summary>
    /// Resolves a step's <c>operationPath</c> to its operation. The value is a runtime expression of
    /// the form <c>{$sourceDescriptions.&lt;name&gt;.url}#/paths/&lt;json-pointer-escaped-path&gt;/&lt;method&gt;</c>;
    /// the JSON Pointer fragment identifies the operation within the (already bound) source description.
    /// </summary>
    /// <param name="operationPath">The <c>operationPath</c> expression.</param>
    /// <param name="operation">When this method returns <see langword="true"/>, the resolved operation.</param>
    /// <returns><see langword="true"/> if the pointer addresses an operation in the document; otherwise <see langword="false"/>.</returns>
    public bool TryResolveOperationPath(string operationPath, out ResolvedOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operationPath);
        operation = default;

        int hash = operationPath.IndexOf('#', StringComparison.Ordinal);
        if (hash < 0)
        {
            return false;
        }

        ReadOnlySpan<char> pointer = operationPath.AsSpan(hash + 1);
        if (pointer.Length == 0 || pointer[0] != '/')
        {
            return false;
        }

        // Expect /paths/<escaped-path>/<method> — derive the path template and method from the
        // tokens, then resolve against the generator's operation list for the authoritative binding.
        ReadOnlySpan<char> rest = pointer[1..];
        int firstSlash = rest.IndexOf('/');
        if (firstSlash < 0 || !rest[..firstSlash].SequenceEqual("paths"))
        {
            return false;
        }

        rest = rest[(firstSlash + 1)..];
        int methodSlash = rest.LastIndexOf('/');
        if (methodSlash <= 0)
        {
            return false;
        }

        string path = UnescapePointerToken(rest[..methodSlash]);
        if (!TryParseMethod(rest[(methodSlash + 1)..], out OperationMethod method))
        {
            return false;
        }

        if (this.byPathMethod.TryGetValue(new PathMethod(path, method), out OperationDescriptor descriptor))
        {
            operation = new ResolvedOperation(this.sourceName, descriptor);
            return true;
        }

        return false;
    }

    private static bool TryParseMethod(ReadOnlySpan<char> token, out OperationMethod method)
    {
        // HTTP method names are ASCII; an ordinal-ignore-case compare against the enum names is exact.
        foreach (OperationMethod candidate in Enum.GetValues<OperationMethod>())
        {
            if (token.Equals(candidate.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                method = candidate;
                return true;
            }
        }

        method = default;
        return false;
    }

    private static string UnescapePointerToken(ReadOnlySpan<char> token)
    {
        if (token.IndexOf('~') < 0)
        {
            return token.ToString();
        }

        // RFC 6901: ~1 -> '/', ~0 -> '~' (decode ~1 first so an encoded '~' is not re-interpreted).
        return token.ToString().Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }

    private readonly record struct PathMethod(string Path, OperationMethod Method);
}