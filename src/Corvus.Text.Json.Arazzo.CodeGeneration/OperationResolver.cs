// <copyright file="OperationResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Resolves the operations a workflow step targets against a single source description's OpenAPI
/// document, mapping a step's <c>operationId</c> or <c>operationPath</c> to the (path, method) pair
/// the generator binds to a generated request/response type (plan §3.1).
/// </summary>
public sealed class OperationResolver
{
    private readonly string sourceName;
    private readonly JsonElement specRoot;
    private readonly Dictionary<string, OperationSummary> byOperationId;

    private OperationResolver(string sourceName, JsonElement specRoot, OperationSummary[] operations)
    {
        this.sourceName = sourceName;
        this.specRoot = specRoot;
        this.byOperationId = new Dictionary<string, OperationSummary>(StringComparer.Ordinal);
        foreach (OperationSummary operation in operations)
        {
            if (operation.OperationId is { } id)
            {
                // Arazzo requires operationId to be unique across the referenced description; the
                // first declaration wins, matching how clients resolve duplicates.
                this.byOperationId.TryAdd(id, operation);
            }
        }
    }

    /// <summary>
    /// Creates a resolver for a source description's OpenAPI document.
    /// </summary>
    /// <param name="sourceName">The <c>name</c> of the source description.</param>
    /// <param name="specRoot">The root element of the parsed OpenAPI document.</param>
    /// <param name="specVersion">
    /// The OpenAPI version (<c>3.0</c>, <c>3.1</c>, or <c>3.2</c>), or <see langword="null"/> to
    /// detect it from the document's <c>openapi</c> field.
    /// </param>
    /// <returns>The resolver.</returns>
    public static OperationResolver Create(string sourceName, JsonElement specRoot, string? specVersion = null)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        string version = specVersion ?? DetectSpecVersion(specRoot);
        OperationSummary[] operations = version switch
        {
            "3.0" => OpenApi30CodeGenerator.ListOperations(specRoot),
            "3.2" => OpenApi32CodeGenerator.ListOperations(specRoot),
            _ => OpenApi31CodeGenerator.ListOperations(specRoot),
        };

        return new OperationResolver(sourceName, specRoot, operations);
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
        if (this.byOperationId.TryGetValue(operationId, out OperationSummary summary))
        {
            operation = new ResolvedOperation(this.sourceName, summary.Path, summary.Method, summary.OperationId);
            return true;
        }

        operation = default;
        return false;
    }

    /// <summary>
    /// Resolves a step's <c>operationPath</c> to its operation. The value is a runtime expression of
    /// the form <c>{$sourceDescriptions.&lt;name&gt;.url}#/paths/&lt;json-pointer-escaped-path&gt;/&lt;method&gt;</c>;
    /// only the JSON Pointer fragment is interpreted here (the source is already bound to this resolver).
    /// </summary>
    /// <param name="operationPath">The <c>operationPath</c> expression.</param>
    /// <param name="operation">When this method returns <see langword="true"/>, the resolved operation.</param>
    /// <returns><see langword="true"/> if the pointer resolves to an operation in the document; otherwise <see langword="false"/>.</returns>
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

        // Confirm the pointer actually addresses a node in the document.
        if (!this.specRoot.TryResolvePointer(pointer.ToString(), out JsonElement target)
            || target.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Expect /paths/<escaped-path>/<method> — derive the path template and method from the
        // last two tokens (the method token names the operation within the path item).
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
        ReadOnlySpan<char> methodToken = rest[(methodSlash + 1)..];
        if (!TryParseMethod(methodToken, out OperationMethod method))
        {
            return false;
        }

        string? operationId = target.TryGetProperty("operationId"u8, out JsonElement idElement)
            && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString()
                : null;

        operation = new ResolvedOperation(this.sourceName, path, method, operationId);
        return true;
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

    private static string DetectSpecVersion(JsonElement specRoot)
    {
        if (specRoot.TryGetProperty("openapi"u8, out JsonElement version)
            && version.ValueKind == JsonValueKind.String)
        {
            string? v = version.GetString();
            if (v?.StartsWith("3.0", StringComparison.Ordinal) == true)
            {
                return "3.0";
            }

            if (v?.StartsWith("3.2", StringComparison.Ordinal) == true)
            {
                return "3.2";
            }
        }

        return "3.1";
    }
}