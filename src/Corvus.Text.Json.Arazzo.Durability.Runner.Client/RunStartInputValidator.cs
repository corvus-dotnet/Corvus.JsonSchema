// <copyright file="RunStartInputValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using ValidatorSchema = Corvus.Text.Json.Validator.JsonSchema;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Validates a sealed start's inputs against the version's declared inputs schema at first claim (ADR 0065 decision
/// 9): the control plane admitted ciphertext it could not check, so the runner checks what it opened, against the
/// same schema the control plane builds for a clear start (<see cref="WorkflowValidationSchema"/>), from the
/// version's own workflow document as this runner's artifact source serves it. A version that declares no inputs
/// schema admits any inputs, as the control plane would have.
/// </summary>
public sealed class RunStartInputValidator
{
    private const int MaxCachedSchemas = 256;
    private static readonly ValidatorSchema.Options ConfinedSchemaResolution = new(allowFileSystemAndHttpResolution: false);

    private readonly IWorkflowArtifactSource artifacts;
    private readonly ConcurrentDictionary<string, ValidatorSchema?> schemas = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="RunStartInputValidator"/> class.</summary>
    /// <param name="artifacts">Where the runner reads a hosted version's documents from.</param>
    public RunStartInputValidator(IWorkflowArtifactSource artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        this.artifacts = artifacts;
    }

    /// <summary>Validates a run's inputs against its version's inputs schema.</summary>
    /// <param name="workflowId">The versioned workflow id the run executes.</param>
    /// <param name="inputs">The opened inputs.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the inputs validate, or the version declares no inputs schema.</returns>
    /// <exception cref="InvalidOperationException">The version's workflow document is not one this runner can read.</exception>
    public async ValueTask<bool> ValidateAsync(string workflowId, JsonElement inputs, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        if (!this.schemas.TryGetValue(workflowId, out ValidatorSchema? schema))
        {
            schema = await this.BuildAsync(workflowId, cancellationToken).ConfigureAwait(false);
            if (this.schemas.Count >= MaxCachedSchemas)
            {
                this.schemas.Clear();
            }

            this.schemas[workflowId] = schema;
        }

        if (schema is null)
        {
            return true;
        }

        // A start with no inputs at all is validated as the empty object it amounts to, as the control plane does.
        using ParsedJsonDocument<JsonElement>? empty = inputs.ValueKind == JsonValueKind.Undefined ? ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray()) : null;
        JsonElement validated = empty is { } e ? e.RootElement : inputs;
        return schema.Value.Validate(in validated);
    }

    private async ValueTask<ValidatorSchema?> BuildAsync(string workflowId, CancellationToken cancellationToken)
    {
        if (!WorkflowVersionId.TryParse(workflowId, out string baseWorkflowId, out int versionNumber))
        {
            throw new InvalidOperationException($"'{workflowId}' is not a versioned workflow id, so its inputs schema cannot be resolved.");
        }

        ReadOnlyMemory<byte> workflow = await this.artifacts.GetDocumentAsync(baseWorkflowId, versionNumber, CatalogPackage.WorkflowDocumentName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Version {versionNumber} of '{baseWorkflowId}' has no workflow document this runner can read, so a sealed start's inputs cannot be validated.");
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(workflow);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Utf8JsonWriter(buffer);
        if (!WorkflowValidationSchema.TryWriteInputs(writer, document.RootElement, workflowId))
        {
            return null;
        }

        writer.Flush();

        // Confined to the document supplied here, as the control plane confines it: a $ref in a tenant's inputs
        // schema must not become a file read or a request from the runner.
        return ValidatorSchema.FromText(Encoding.UTF8.GetString(buffer.WrittenSpan), "corvus:runner/" + workflowId, ConfinedSchemaResolution);
    }
}