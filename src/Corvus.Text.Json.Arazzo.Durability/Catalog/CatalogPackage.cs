// <copyright file="CatalogPackage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using System.Text.RegularExpressions;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The catalog's processing of a <see cref="WorkflowPackage"/> archive: reading the base workflow id, projecting
/// a submitted package into the stored form (the workflow id rewritten to the versioned form, the archive
/// repacked canonically and content-hashed, and the searchable metadata extracted), slicing an individual
/// document out, and the offline build / unpack / verify the CLI uses. The package itself is an opaque ZIP
/// (see <see cref="WorkflowPackage"/>); this type is the catalog-facing logic over it.
/// </summary>
public static partial class CatalogPackage
{
    /// <summary>The reserved document name addressing the package's Arazzo workflow document.</summary>
    public const string WorkflowDocumentName = WorkflowPackage.WorkflowDocumentName;

    private const string WorkflowsProperty = "workflows";
    private const string WorkflowIdProperty = "workflowId";
    private const string InfoProperty = "info";
    private const string SourceDescriptionsProperty = "sourceDescriptions";

    /// <summary>Determines whether a workflow id already carries a <c>-vN</c> version suffix (a collision-defence reject).</summary>
    /// <param name="workflowId">The workflow id to test.</param>
    /// <returns><see langword="true"/> if the id ends with <c>-v</c> followed by digits.</returns>
    public static bool IsVersioned(string workflowId)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        return VersionSuffix().IsMatch(workflowId);
    }

    /// <summary>
    /// Reads the base workflow id from a submitted package — the first workflow's <c>workflowId</c>. This is the
    /// id the submission must carry (without a <c>-vN</c> suffix).
    /// </summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The base workflow id.</returns>
    /// <exception cref="ArgumentException">The package is malformed or carries no workflow id.</exception>
    public static string ReadBaseWorkflowId(ReadOnlyMemory<byte> packageZip)
    {
        WorkflowPackageContents contents = WorkflowPackage.Open(packageZip);
        using ParsedJsonDocument<JsonElement> workflow = ParsedJsonDocument<JsonElement>.Parse(contents.Workflow);
        if (TryReadWorkflowId(workflow.RootElement, out string? workflowId))
        {
            return workflowId;
        }

        throw new ArgumentException("The package's Arazzo workflow has no workflowId.", nameof(packageZip));
    }

    /// <summary>
    /// Processes a submitted package into the stored form: rewrites the first workflow's id to
    /// <c>{baseWorkflowId}-v{versionNumber}</c>, repacks the archive canonically, content-hashes it, and projects
    /// the searchable metadata.
    /// </summary>
    /// <param name="packageZip">The submitted package archive bytes.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number assigned by the store.</param>
    /// <param name="metadataProvider">An optional provider that precomputes the typed schema-metadata document
    /// baked into the canonical package (<see cref="WorkflowPackage.SchemasEntryName"/>); <see langword="null"/>
    /// to omit it. The content hash is unaffected (it covers only the workflow + sources).</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked
    /// into the canonical package (<see cref="WorkflowPackage.ExecutorEntryName"/> + manifest);
    /// <see langword="null"/> to omit it. The content hash is unaffected.</param>
    /// <returns>The projection (canonical package archive bytes, versioned id, hash, title, description, sources).</returns>
    public static CatalogPackageProjection Project(ReadOnlyMemory<byte> packageZip, string baseWorkflowId, int versionNumber, IWorkflowMetadataProvider? metadataProvider = null, IWorkflowExecutorProvider? executorProvider = null)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        string workflowId = $"{baseWorkflowId}-v{versionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        WorkflowPackageContents contents = WorkflowPackage.Open(packageZip);
        byte[] rewrittenWorkflow = RewriteWorkflowId(contents.Workflow, workflowId);
        string hash = WorkflowPackage.ComputeContentHash(rewrittenWorkflow, contents.Sources);
        ReadOnlyMemory<byte> schemas = metadataProvider?.BuildSchemas(rewrittenWorkflow, contents.Sources) ?? default;
        WorkflowExecutorArtifact? executor = executorProvider?.BuildExecutor(rewrittenWorkflow, contents.Sources, hash);
        byte[] canonicalPackage = WorkflowPackage.Pack(rewrittenWorkflow, contents.Sources, schemas, executor?.Assembly ?? default, executor?.Manifest ?? default);

        string title;
        string? description;
        IReadOnlyList<CatalogSourceRef> sources;
        using (ParsedJsonDocument<JsonElement> workflow = ParsedJsonDocument<JsonElement>.Parse(rewrittenWorkflow))
        {
            (title, description) = ReadTitleAndDescription(workflow.RootElement);
            sources = ReadSources(workflow.RootElement);
        }

        return new CatalogPackageProjection(canonicalPackage, workflowId, hash, title, description, sources, executor.HasValue);
    }

    /// <summary>
    /// Slices a single document out of a stored package: <see cref="WorkflowDocumentName"/> returns the Arazzo
    /// workflow document, any other name returns the matching source document.
    /// </summary>
    /// <param name="packageZip">The stored package archive bytes.</param>
    /// <param name="documentName">The document name to extract.</param>
    /// <returns>The document's JSON bytes, or <see langword="null"/> if no such document is present.</returns>
    public static ReadOnlyMemory<byte>? GetDocument(ReadOnlyMemory<byte> packageZip, string documentName)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        byte[]? document = WorkflowPackage.Open(packageZip).GetDocument(documentName);
        return document is null ? null : (ReadOnlyMemory<byte>?)document;
    }

    /// <summary>
    /// Assembles a package archive from an Arazzo workflow document and its referenced source documents. Used by
    /// the CLI's offline <c>pack</c> (and inline <c>add</c>) to produce the upload artifact from local files.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by their <c>sourceDescriptions</c> name.</param>
    /// <returns>The package archive bytes.</returns>
    public static byte[] Build(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
        => WorkflowPackage.Pack(workflowUtf8, sources);

    /// <summary>Unpacks a package archive into its constituent documents — the inverse of <see cref="Build"/>.</summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The Arazzo workflow document bytes and the source documents (name → bytes).</returns>
    /// <exception cref="ArgumentException">The package has no workflow document.</exception>
    public static (byte[] Workflow, IReadOnlyList<KeyValuePair<string, byte[]>> Sources) Unpack(ReadOnlyMemory<byte> packageZip)
    {
        WorkflowPackageContents contents = WorkflowPackage.Open(packageZip);
        return (contents.Workflow, contents.Sources);
    }

    /// <summary>Computes the SHA-256 (lower-hex) of a package's canonical content (container-independent).</summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The hex-encoded hash — comparable to a catalog version's reported <c>hash</c>.</returns>
    public static string HashCanonical(ReadOnlyMemory<byte> packageZip)
    {
        WorkflowPackageContents contents = WorkflowPackage.Open(packageZip);
        return WorkflowPackage.ComputeContentHash(contents.Workflow, contents.Sources);
    }

    /// <summary>
    /// Validates a package archive locally: that it is well-formed (a workflow document with a workflow id), that
    /// the workflow id carries no <c>-vN</c> suffix, and that every non-<c>arazzo</c> <c>sourceDescriptions</c>
    /// entry has a matching source document. Also recomputes the content hash and projects the sources.
    /// </summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The validation result (issues, base workflow id, hash, sources).</returns>
    public static CatalogPackageValidation Validate(ReadOnlyMemory<byte> packageZip)
    {
        var issues = new List<string>();
        string? baseWorkflowId = null;
        IReadOnlyList<CatalogSourceRef> sources = [];
        string hash = string.Empty;

        try
        {
            WorkflowPackageContents contents = WorkflowPackage.Open(packageZip);
            hash = WorkflowPackage.ComputeContentHash(contents.Workflow, contents.Sources);

            using ParsedJsonDocument<JsonElement> workflow = ParsedJsonDocument<JsonElement>.Parse(contents.Workflow);
            sources = ReadSources(workflow.RootElement);

            if (TryReadWorkflowId(workflow.RootElement, out string? workflowId))
            {
                baseWorkflowId = workflowId;
                if (IsVersioned(workflowId))
                {
                    issues.Add($"The workflow id '{workflowId}' already carries a version suffix; submit the base id without '-vN'.");
                }
            }
            else
            {
                issues.Add("The Arazzo workflow has no workflowId.");
            }

            var present = new HashSet<string>(contents.Sources.Select(s => s.Key), StringComparer.Ordinal);
            foreach (CatalogSourceRef source in sources)
            {
                if (!string.Equals(source.Type, "arazzo", StringComparison.OrdinalIgnoreCase) && !present.Contains(source.Name))
                {
                    issues.Add($"Source '{source.Name}' is declared in sourceDescriptions but absent from the package.");
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            issues.Add($"The package is not a valid archive: {ex.Message}");
        }

        return new CatalogPackageValidation(issues.Count == 0, issues, baseWorkflowId, hash, sources);
    }

    private static bool TryReadWorkflowId(JsonElement workflow, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? workflowId)
    {
        if (workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty(WorkflowsProperty, out JsonElement workflows)
            && workflows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement wf in workflows.EnumerateArray())
            {
                if (wf.TryGetProperty(WorkflowIdProperty, out JsonElement id) && id.GetString() is { Length: > 0 } value)
                {
                    workflowId = value;
                    return true;
                }

                break;
            }
        }

        workflowId = null;
        return false;
    }

    private static (string Title, string? Description) ReadTitleAndDescription(JsonElement workflow)
    {
        string title = string.Empty;
        string? description = null;
        if (workflow.ValueKind == JsonValueKind.Object && workflow.TryGetProperty(InfoProperty, out JsonElement info) && info.ValueKind == JsonValueKind.Object)
        {
            if (info.TryGetProperty("title", out JsonElement t) && t.GetString() is { } titleValue)
            {
                title = titleValue;
            }

            if (info.TryGetProperty("description", out JsonElement d) && d.GetString() is { Length: > 0 } descriptionValue)
            {
                description = descriptionValue;
            }
            else if (info.TryGetProperty("summary", out JsonElement s) && s.GetString() is { Length: > 0 } summaryValue)
            {
                description = summaryValue;
            }
        }

        return (title, description);
    }

    private static IReadOnlyList<CatalogSourceRef> ReadSources(JsonElement workflow)
    {
        if (workflow.ValueKind != JsonValueKind.Object
            || !workflow.TryGetProperty(SourceDescriptionsProperty, out JsonElement descriptions)
            || descriptions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var sources = new List<CatalogSourceRef>();
        foreach (JsonElement source in descriptions.EnumerateArray())
        {
            if (source.TryGetProperty("name", out JsonElement name) && name.GetString() is { Length: > 0 } nameValue)
            {
                string? type = source.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
                sources.Add(new CatalogSourceRef(nameValue, type));
            }
        }

        return sources;
    }

    private static byte[] RewriteWorkflowId(ReadOnlyMemory<byte> workflowUtf8, string newWorkflowId)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8);
        return PersistedJson.ToArray(
            (Root: document.RootElement, NewWorkflowId: newWorkflowId),
            static (Utf8JsonWriter writer, in (JsonElement Root, string NewWorkflowId) c) => WriteWorkflowWithId(c.Root, writer, c.NewWorkflowId));
    }

    private static void WriteWorkflowWithId(JsonElement workflow, Utf8JsonWriter writer, string newWorkflowId)
    {
        writer.WriteStartObject();
        foreach (JsonProperty<JsonElement> property in workflow.EnumerateObject())
        {
            if (property.Name == WorkflowsProperty && property.Value.ValueKind == JsonValueKind.Array)
            {
                writer.WritePropertyName(WorkflowsProperty);
                writer.WriteStartArray();
                int index = 0;
                foreach (JsonElement element in property.Value.EnumerateArray())
                {
                    if (index == 0 && element.ValueKind == JsonValueKind.Object)
                    {
                        WriteWorkflowEntryWithId(element, writer, newWorkflowId);
                    }
                    else
                    {
                        element.WriteTo(writer);
                    }

                    index++;
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteWorkflowEntryWithId(JsonElement entry, Utf8JsonWriter writer, string newWorkflowId)
    {
        writer.WriteStartObject();
        foreach (JsonProperty<JsonElement> property in entry.EnumerateObject())
        {
            if (property.Name == WorkflowIdProperty)
            {
                writer.WriteString(WorkflowIdProperty, newWorkflowId);
            }
            else
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    [GeneratedRegex(@"-v\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionSuffix();
}

/// <summary>The stored form of a submitted package plus its projected searchable metadata.</summary>
/// <param name="CanonicalPackage">The canonical package archive bytes (workflow id rewritten), as stored.</param>
/// <param name="WorkflowId">The versioned workflow id written into the package.</param>
/// <param name="Hash">The SHA-256 of the package's canonical content, lower-hex-encoded.</param>
/// <param name="Title">The title from the workflow's <c>info.title</c>.</param>
/// <param name="Description">The description (from <c>info.description</c>, falling back to <c>info.summary</c>), if any.</param>
/// <param name="Sources">The source documents declared by the workflow (name + type).</param>
/// <param name="HasExecutor">Whether a compiled workflow executor assembly was baked into the canonical package
/// (i.e. an executor provider was supplied and produced one) — the version is runnable.</param>
public readonly record struct CatalogPackageProjection(
    ReadOnlyMemory<byte> CanonicalPackage,
    string WorkflowId,
    string Hash,
    string Title,
    string? Description,
    IReadOnlyList<CatalogSourceRef> Sources,
    bool HasExecutor = false);

/// <summary>The result of a local <see cref="CatalogPackage.Validate"/> of a package archive.</summary>
/// <param name="IsValid">Whether the package is well-formed and referentially complete (no <paramref name="Issues"/>).</param>
/// <param name="Issues">The validation issues found; empty when valid.</param>
/// <param name="BaseWorkflowId">The workflow id read from the package, if present (the base id for a submission).</param>
/// <param name="Hash">The SHA-256 (lower-hex) of the package's canonical content.</param>
/// <param name="Sources">The source documents declared by the workflow (name + type).</param>
public readonly record struct CatalogPackageValidation(
    bool IsValid,
    IReadOnlyList<string> Issues,
    string? BaseWorkflowId,
    string Hash,
    IReadOnlyList<CatalogSourceRef> Sources);