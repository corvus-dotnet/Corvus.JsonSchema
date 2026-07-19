// <copyright file="WorkflowStepOutputsModelGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Generates a strongly-typed C# model for a step's resolved <c>outputs</c> — an object whose properties are the
/// step's declared outputs, each carrying the schema resolved from its source expression — and the accessor map
/// (output JSON name → generated dotnet property) the executor emitter needs to compile
/// <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c> to a strongly-typed accessor (#872).
/// </summary>
/// <remarks>
/// The outputs schema is derived by <see cref="WorkflowSchemaMetadataGenerator.TryBuildValidationSchema"/> with a
/// <see cref="WorkflowSchemaTargetKind.StepOutputs"/> target — the same expression→schema resolution used for the
/// step's validation metadata — and produces a self-contained schema document (it carries the source and workflow
/// reusable schema buckets so local <c>$ref</c>s resolve). This drives the Corvus JSON Schema code generator over
/// that schema exactly as <see cref="WorkflowInputsModelGenerator"/> does for the workflow inputs. An output whose
/// source cannot be resolved to a single schema (e.g. a pointer through a <c>oneOf</c>) degrades to an open schema
/// and hence a permissive <c>JsonAny</c> property — typed where determinable, permissive at genuine ambiguity,
/// never loose.
/// </remarks>
public static class WorkflowStepOutputsModelGenerator
{
    /// <summary>
    /// Generates the outputs model for a single step.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents (name → UTF-8 JSON bytes), keyed by their
    /// <c>sourceDescriptions</c> name — the resolver needs them to type response/payload outputs.</param>
    /// <param name="workflowId">The workflow id; <see langword="null"/> selects the first workflow.</param>
    /// <param name="stepId">The step whose outputs to type.</param>
    /// <param name="modelNamespace">The namespace for the generated model types (per step, so names never collide).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated model (type name, accessor map, and source files), or <see langword="null"/> when the
    /// step declares no outputs (or the outputs schema is not resolvable to a named type).</returns>
    public static async ValueTask<WorkflowStepOutputsModel?> GenerateAsync(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        string? workflowId,
        string stepId,
        string modelNamespace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentException.ThrowIfNullOrEmpty(modelNamespace);

        // Derive the step's outputs schema ({outputName: resolvedSchema}) — the same resolution the validation
        // metadata uses — then generate a typed model from it, exactly as the inputs model is generated.
        if (!WorkflowSchemaMetadataGenerator.TryBuildValidationSchema(
                workflowUtf8,
                sources,
                new WorkflowSchemaTarget(WorkflowSchemaTargetKind.StepOutputs, workflowId, stepId),
                out byte[] schemaDocument))
        {
            return null;
        }

        DerivedSchemaModel? model = await DerivedSchemaModelGenerator
            .GenerateAsync(schemaDocument, modelNamespace, cancellationToken)
            .ConfigureAwait(false);

        return model is null ? null : new WorkflowStepOutputsModel(model.TypeName, model.Accessors, model.Files);
    }
}

/// <summary>
/// The generated outputs model for a step.
/// </summary>
/// <param name="TypeName">The fully-qualified generated outputs type name (a typed view over the step's outputs object).</param>
/// <param name="Accessors">Map of output JSON name → generated dotnet accessor property (e.g. <c>petId</c> → <c>PetId</c>).</param>
/// <param name="Files">The generated model source files.</param>
public sealed record WorkflowStepOutputsModel(
    string TypeName,
    IReadOnlyDictionary<string, string> Accessors,
    IReadOnlyList<GeneratedModelFile> Files);