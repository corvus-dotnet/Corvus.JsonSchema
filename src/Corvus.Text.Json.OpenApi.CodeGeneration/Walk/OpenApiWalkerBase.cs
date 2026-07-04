// <copyright file="OpenApiWalkerBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The shared, version-neutral base of the OpenAPI walk phase.
/// </summary>
/// <remarks>
/// <para>
/// A <em>walker</em> turns a strongly-typed OpenAPI document into the language-neutral
/// intermediate representation (a list of <see cref="OperationInfo"/>). The version-specific
/// traversal of the typed model lives in a per-version subclass (it dereferences a typed model
/// that this shared assembly cannot reference); this base owns the entry point and the
/// version-agnostic shaping helpers whose bodies operate only on raw <see cref="JsonElement"/>
/// values, the intermediate representation, or primitive data.
/// </para>
/// <para>
/// The walk is callable independently of any emit phase: <see cref="Walk"/> returns the shared
/// intermediate representation that any downstream emitter (C#, TypeScript, …) can consume.
/// </para>
/// </remarks>
public abstract class OpenApiWalkerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiWalkerBase"/> class.
    /// </summary>
    /// <param name="clientNamePrefix">
    /// Optional prefix for client type names. If <see langword="null"/>, <c>"Api"</c> is used.
    /// </param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    protected OpenApiWalkerBase(string? clientNamePrefix, bool ignoreEmptyFormUrlEncodedBody)
    {
        this.ClientNamePrefix = clientNamePrefix;
        this.IgnoreEmptyFormUrlEncodedBody = ignoreEmptyFormUrlEncodedBody;
    }

    /// <summary>
    /// Gets the optional prefix for client type names.
    /// </summary>
    protected string? ClientNamePrefix { get; }

    /// <summary>
    /// Gets a value indicating whether empty form-urlencoded request bodies are ignored.
    /// </summary>
    protected bool IgnoreEmptyFormUrlEncodedBody { get; }

    /// <summary>
    /// Walks the OpenAPI specification's path operations and produces the language-neutral
    /// intermediate representation.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="filter">Optional operation filter.</param>
    /// <param name="referenceResolver">
    /// Optional reference resolver. If <see langword="null"/>, a
    /// <see cref="LocalReferenceResolver"/> is used.
    /// </param>
    /// <returns>The operations discovered under <c>paths</c>, as intermediate representation.</returns>
    public IReadOnlyList<OperationInfo> Walk(
        JsonElement specRoot,
        OperationFilter? filter = null,
        IOpenApiReferenceResolver? referenceResolver = null)
    {
        referenceResolver ??= new LocalReferenceResolver(specRoot);
        return this.WalkOperations(specRoot, filter, referenceResolver);
    }

    /// <summary>
    /// Lists all tags defined in the top-level <c>tags</c> array of a specification.
    /// </summary>
    /// <remarks>
    /// The <c>parent</c> and <c>kind</c> fields are new in OpenAPI 3.2, but this method will
    /// still read them if present (e.g. from extension usage in earlier specs).
    /// </remarks>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <returns>An array of <see cref="TagInfo"/> records.</returns>
    public static TagInfo[] ListTags(JsonElement specRoot)
    {
        if (!specRoot.TryGetProperty("tags"u8, out JsonElement tagsElement)
            || tagsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<TagInfo> result = [];
        foreach (JsonElement tag in tagsElement.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? name = tag.TryGetProperty("name"u8, out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;

            if (name is null)
            {
                continue;
            }

            string? summary = tag.TryGetProperty("summary"u8, out JsonElement summaryEl) && summaryEl.ValueKind == JsonValueKind.String
                ? summaryEl.GetString()
                : null;

            string? description = tag.TryGetProperty("description"u8, out JsonElement descEl) && descEl.ValueKind == JsonValueKind.String
                ? descEl.GetString()
                : null;

            string? parent = tag.TryGetProperty("parent"u8, out JsonElement parentEl) && parentEl.ValueKind == JsonValueKind.String
                ? parentEl.GetString()
                : null;

            string? kind = tag.TryGetProperty("kind"u8, out JsonElement kindEl) && kindEl.ValueKind == JsonValueKind.String
                ? kindEl.GetString()
                : null;

            result.Add(new TagInfo(name, summary, description, parent, kind));
        }

        return [.. result];
    }

    /// <summary>
    /// Builds a map of security-scheme name to its OpenAPI <c>type</c> by reading
    /// <c>components.securitySchemes</c> directly from the raw document. Reading raw JSON keeps this
    /// version-agnostic and tolerant of the differing typed models across OpenAPI versions.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <returns>A map from security-scheme name to its declared <c>type</c>.</returns>
    public static Dictionary<string, string> BuildSecuritySchemeTypeLookup(JsonElement specRoot)
    {
        Dictionary<string, string> lookup = new(StringComparer.Ordinal);

        if (specRoot.ValueKind != JsonValueKind.Object
            || !specRoot.TryGetProperty("components"u8, out JsonElement components)
            || components.ValueKind != JsonValueKind.Object
            || !components.TryGetProperty("securitySchemes"u8, out JsonElement schemes)
            || schemes.ValueKind != JsonValueKind.Object)
        {
            return lookup;
        }

        foreach (var scheme in schemes.EnumerateObject())
        {
            if (scheme.Value.ValueKind == JsonValueKind.Object
                && scheme.Value.TryGetProperty("type"u8, out JsonElement schemeType)
                && schemeType.ValueKind == JsonValueKind.String)
            {
                lookup[scheme.Name] = schemeType.GetString()!;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Synthesizes a client method name for an operation.
    /// </summary>
    /// <param name="operationId">The optional <c>operationId</c>.</param>
    /// <param name="method">The HTTP method of the operation.</param>
    /// <param name="pathTemplate">The path template (e.g. <c>/pets/{petId}</c>).</param>
    /// <param name="customMethodName">
    /// The custom HTTP method name (OpenAPI 3.2 <c>additionalOperations</c>), used only when
    /// <paramref name="method"/> is <see cref="OperationMethod.Custom"/>.
    /// </param>
    /// <returns>The Pascal-cased method name.</returns>
    public static string GetMethodName(
        string? operationId,
        OperationMethod method,
        string pathTemplate,
        string? customMethodName = null)
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
            OperationMethod.Query => "query",
            OperationMethod.Custom => customMethodName?.ToLowerInvariant() ?? "custom",
            _ => throw new UnreachableException(),
        };

        string pathPart = pathTemplate
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        return CodeEmitHelpers.ToPascalCase(methodPrefix + " " + pathPart);
    }

    /// <summary>
    /// Groups operations by tag (operations with no tag fall under <c>"default"</c>; an operation
    /// with multiple tags appears under each of them).
    /// </summary>
    /// <param name="operations">The operations to group.</param>
    /// <returns>A map from tag to the operations that carry it.</returns>
    public static Dictionary<string, List<OperationInfo>> GroupOperationsByTag(
        List<OperationInfo> operations)
    {
        Dictionary<string, List<OperationInfo>> groups = new(StringComparer.Ordinal);

        foreach (OperationInfo op in operations)
        {
            if (op.Tags.Length == 0)
            {
                AddToGroup(groups, "default", op);
            }
            else
            {
                foreach (string tag in op.Tags)
                {
                    AddToGroup(groups, tag, op);
                }
            }
        }

        return groups;

        static void AddToGroup(
            Dictionary<string, List<OperationInfo>> groups,
            string tag,
            OperationInfo op)
        {
            if (!groups.TryGetValue(tag, out List<OperationInfo>? list))
            {
                list = [];
                groups[tag] = list;
            }

            list.Add(op);
        }
    }

    /// <summary>
    /// Synthesizes a client type name for a tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The client type name (prefix + sanitized tag).</returns>
    public string GetClientName(string tag)
    {
        string prefix = this.ClientNamePrefix ?? "Api";
        string sanitized = CodeEmitHelpers.SanitizeIdentifier(tag);
        return $"{prefix}{sanitized}";
    }

    /// <summary>
    /// Walks the typed model of a specific OpenAPI version into the shared intermediate
    /// representation. Implemented by a per-version subclass that can reference the typed model.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="filter">Optional operation filter.</param>
    /// <param name="referenceResolver">The reference resolver (never <see langword="null"/>).</param>
    /// <returns>The operations discovered under <c>paths</c>, as intermediate representation.</returns>
    protected abstract IReadOnlyList<OperationInfo> WalkOperations(
        JsonElement specRoot,
        OperationFilter? filter,
        IOpenApiReferenceResolver referenceResolver);
}