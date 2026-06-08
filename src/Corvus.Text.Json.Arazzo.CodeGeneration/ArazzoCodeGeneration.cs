// <copyright file="ArazzoCodeGeneration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo10;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Generates strongly-typed workflow executors (and their inputs models) from an Arazzo document.
/// </summary>
/// <remarks>
/// The generator walks an Arazzo document together with its resolved source descriptions (supplied as a
/// <see cref="WorkflowOperationBinder"/>), and for each workflow emits an inputs model — see
/// <see cref="WorkflowInputsModelGenerator"/> — and an executor class — see
/// <see cref="WorkflowExecutorEmitter"/>. It performs no file I/O: it returns the generated sources so
/// the caller (e.g. an <c>arazzo generate</c> CLI command) decides where to write them.
/// </remarks>
public static class ArazzoCodeGeneration
{
    /// <summary>
    /// The default .NET namespace suffix for generated workflow executors.
    /// </summary>
    public const string DefaultWorkflowsNamespaceSuffix = "Workflows";

    /// <summary>
    /// The default .NET namespace suffix for generated inputs models.
    /// </summary>
    public const string DefaultModelsNamespaceSuffix = "Models";

    /// <summary>
    /// Generates the inputs models and executor classes for every workflow in an Arazzo document.
    /// </summary>
    /// <param name="arazzoDocumentUtf8">The Arazzo document as UTF-8 JSON.</param>
    /// <param name="binder">The operation binder resolving each step to a generated operation (built from the document's source descriptions).</param>
    /// <param name="options">The generation options (root namespace, outputs type).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated source files, each with a relative path. Inputs models live under <c>Models/&lt;Workflow&gt;/</c>; executors under <c>Workflows/</c>.</returns>
    public static async ValueTask<IReadOnlyList<GeneratedModelFile>> GenerateAsync(
        ReadOnlyMemory<byte> arazzoDocumentUtf8,
        WorkflowOperationBinder binder,
        ArazzoGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentException.ThrowIfNullOrEmpty(options.RootNamespace);

        var files = new List<GeneratedModelFile>();
        string workflowsNamespace = $"{options.RootNamespace}.{DefaultWorkflowsNamespaceSuffix}";

        using var document = ParsedJsonDocument<ArazzoDocument>.Parse(arazzoDocumentUtf8.ToArray());

        // The document's components hold reusable success/failure actions referenced by $ref from steps
        // or workflow-level action lists.
        JsonElement components = document.RootElement.Components.IsNotUndefined()
            ? document.RootElement.Components
            : default;

        int index = 0;
        foreach (ArazzoDocument.WorkflowObject workflow in document.RootElement.Workflows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string workflowId = workflow.WorkflowId.IsNotUndefined()
                ? workflow.WorkflowId.GetString()!
                : throw new InvalidOperationException($"Workflow at index {index} is missing its required workflowId.");

            string workflowName = EmitText.ToPascalCase(workflowId);
            string className = $"{workflowName}Workflow";

            // Each workflow's inputs model gets its own namespace so same-named generated types (e.g.
            // an "Inputs" object) never collide across workflows. A workflow with no inputs schema uses
            // the untyped JsonElement.
            string modelNamespace = $"{options.RootNamespace}.{DefaultModelsNamespaceSuffix}.{workflowName}";
            WorkflowInputsModel? model = workflow.Inputs.IsNotUndefined()
                ? await WorkflowInputsModelGenerator
                    .GenerateAsync(arazzoDocumentUtf8, index, modelNamespace, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            string inputsTypeName = model?.TypeName ?? "Corvus.Text.Json.JsonElement";
            IReadOnlyDictionary<string, string>? inputAccessors = model?.Accessors;
            if (model is not null)
            {
                foreach (GeneratedModelFile modelFile in model.Files)
                {
                    files.Add(new GeneratedModelFile($"{DefaultModelsNamespaceSuffix}/{workflowName}/{modelFile.FileName}", modelFile.Content));
                }
            }

            string executorSource = WorkflowExecutorEmitter.Emit(
                workflow,
                binder,
                new WorkflowExecutorOptions(workflowsNamespace, className, inputsTypeName, options.OutputsTypeName, inputAccessors),
                components);

            files.Add(new GeneratedModelFile($"{DefaultWorkflowsNamespaceSuffix}/{className}.cs", executorSource));

            index++;
        }

        return files;
    }
}

/// <summary>
/// Options controlling Arazzo workflow generation.
/// </summary>
/// <param name="RootNamespace">The root .NET namespace (executors go under <c>&lt;RootNamespace&gt;.Workflows</c>, inputs models under <c>&lt;RootNamespace&gt;.Models.&lt;Workflow&gt;</c>).</param>
/// <param name="OutputsTypeName">The fully-qualified type of the workflow outputs (defaults to <see cref="JsonElement"/>).</param>
public readonly record struct ArazzoGenerationOptions(
    string RootNamespace,
    string OutputsTypeName = "Corvus.Text.Json.JsonElement");