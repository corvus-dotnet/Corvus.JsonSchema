// <copyright file="WorkflowOutputsModelGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Generates a strongly-typed C# model for a workflow's resolved <c>outputs</c> — its result type: an object whose
/// properties are the workflow's declared outputs, each carrying the schema resolved from its source expression —
/// and the accessor map (output JSON name → generated dotnet property) callers use to read the result (#872).
/// </summary>
/// <remarks>
/// The outputs schema is derived by <see cref="WorkflowSchemaMetadataGenerator.TryBuildValidationSchema"/> with a
/// <see cref="WorkflowSchemaTargetKind.WorkflowOutputs"/> target — each output resolves through the shared output
/// resolver: a step output ($steps.&lt;id&gt;.outputs.&lt;name&gt;, resolved transitively), a workflow input, or an
/// intrinsic. It produces a self-contained schema document (carrying every source's reusable schema buckets so a
/// resolved cross-step schema's <c>$ref</c>s resolve). This drives the Corvus JSON Schema code generator over that
/// schema exactly as <see cref="WorkflowStepOutputsModelGenerator"/> does for a single step's outputs. An output
/// whose source cannot be resolved to a single schema degrades to an open schema and hence a permissive
/// <c>JsonAny</c> property — typed where determinable, permissive at genuine ambiguity, never loose.
/// </remarks>
public static class WorkflowOutputsModelGenerator
{
    /// <summary>
    /// Generates the result model for a workflow.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents (name → UTF-8 JSON bytes), keyed by their
    /// <c>sourceDescriptions</c> name — the resolver needs them to type outputs that read a step's response/payload.</param>
    /// <param name="workflowId">The workflow id; <see langword="null"/> selects the first workflow.</param>
    /// <param name="modelNamespace">The namespace for the generated model types (per workflow, so names never collide).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated model (type name, accessor map, and source files), or <see langword="null"/> when the
    /// workflow declares no outputs (or the outputs schema is not resolvable to a named type).</returns>
    public static async ValueTask<WorkflowOutputsModel?> GenerateAsync(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        string? workflowId,
        string modelNamespace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(modelNamespace);

        // Derive the workflow's outputs schema ({outputName: resolvedSchema}) — the same resolution the validation
        // metadata uses — then generate a typed model from it, exactly as the step outputs model is generated.
        if (!WorkflowSchemaMetadataGenerator.TryBuildValidationSchema(
                workflowUtf8,
                sources,
                new WorkflowSchemaTarget(WorkflowSchemaTargetKind.WorkflowOutputs, workflowId),
                out byte[] schemaDocument))
        {
            return null;
        }

        DerivedSchemaModel? model = await DerivedSchemaModelGenerator
            .GenerateAsync(schemaDocument, modelNamespace, cancellationToken)
            .ConfigureAwait(false);

        return model is null ? null : new WorkflowOutputsModel(model.TypeName, model.Accessors, model.Files);
    }
}

/// <summary>
/// The generated result model for a workflow.
/// </summary>
/// <param name="TypeName">The fully-qualified generated result type name (a typed view over the workflow's outputs object).</param>
/// <param name="Accessors">Map of output JSON name → generated dotnet accessor property (e.g. <c>petId</c> → <c>PetId</c>).</param>
/// <param name="Files">The generated model source files.</param>
public sealed record WorkflowOutputsModel(
    string TypeName,
    IReadOnlyDictionary<string, string> Accessors,
    IReadOnlyList<GeneratedModelFile> Files);