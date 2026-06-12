// <copyright file="ArazzoCodeGeneration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo11;

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

        var workflowDependencies = new List<(string Id, IReadOnlyList<string> DependsOn)>();

        // The document's API (OpenAPI) source names are surfaced on each durable host adapter's descriptor
        // so an execution host can bind an IApiTransport per source. AsyncAPI sources are served by the
        // message transport (flagged separately on the descriptor), not the API transport map, so they are
        // excluded here; a source with no declared type defaults to OpenAPI.
        var sourceNames = new List<string>();
        if (document.RootElement.SourceDescriptions.IsNotUndefined())
        {
            foreach (ArazzoDocument.SourceDescriptionObject source in document.RootElement.SourceDescriptions.EnumerateArray())
            {
                if (source.Name.IsNotUndefined() && source.Name.GetString() is { Length: > 0 } sourceName)
                {
                    string? sourceType = source.Type.IsNotUndefined() ? source.Type.GetString() : null;
                    if (sourceType is null || string.Equals(sourceType, "openapi", StringComparison.Ordinal))
                    {
                        sourceNames.Add(sourceName);
                    }
                }
            }
        }

        int index = 0;
        foreach (ArazzoDocument.WorkflowObject workflow in document.RootElement.Workflows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string workflowId = workflow.WorkflowId.IsNotUndefined()
                ? workflow.WorkflowId.GetString()!
                : throw new InvalidOperationException($"Workflow at index {index} is missing its required workflowId.");

            workflowDependencies.Add((workflowId, WorkflowExecutorEmitter.ReadWorkflowDependsOn(workflow)));

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
                new WorkflowExecutorOptions(workflowsNamespace, className, inputsTypeName, options.OutputsTypeName, inputAccessors, options.Durable, options.SubWorkflowSourceNamespaces, sourceNames),
                components);

            files.Add(new GeneratedModelFile($"{DefaultWorkflowsNamespaceSuffix}/{className}.cs", executorSource));

            index++;
        }

        // Surface the workflow-level dependsOn ordering for an orchestrator: a catalog listing the
        // document's workflow ids in an order that satisfies their dependsOn declarations.
        files.Add(new GeneratedModelFile(
            $"{DefaultWorkflowsNamespaceSuffix}/WorkflowOrder.cs",
            BuildWorkflowOrderSource(workflowsNamespace, OrderWorkflowsByDependency(workflowDependencies))));

        return files;
    }

    /// <summary>
    /// Orders the document's workflow ids so each workflow's same-document <c>dependsOn</c>
    /// dependencies precede it (stable Kahn's algorithm, document order as the tie-break). Cross-document
    /// dependency references (<c>$sourceDescriptions.…</c>) and unknown ids are ignored; a cycle is a
    /// generation-time error.
    /// </summary>
    private static List<string> OrderWorkflowsByDependency(IReadOnlyList<(string Id, IReadOnlyList<string> DependsOn)> workflows)
    {
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < workflows.Count; i++)
        {
            indexById[workflows[i].Id] = i;
        }

        var inDegree = new int[workflows.Count];
        var dependents = new List<int>[workflows.Count];
        for (int i = 0; i < workflows.Count; i++)
        {
            dependents[i] = [];
        }

        for (int i = 0; i < workflows.Count; i++)
        {
            foreach (string dependency in workflows[i].DependsOn)
            {
                if (indexById.TryGetValue(dependency, out int dependencyIndex) && dependencyIndex != i)
                {
                    dependents[dependencyIndex].Add(i);
                    inDegree[i]++;
                }
            }
        }

        var ready = new SortedSet<int>();
        for (int i = 0; i < workflows.Count; i++)
        {
            if (inDegree[i] == 0)
            {
                ready.Add(i);
            }
        }

        var ordered = new List<string>(workflows.Count);
        while (ready.Count > 0)
        {
            int next = ready.Min;
            ready.Remove(next);
            ordered.Add(workflows[next].Id);
            foreach (int dependent in dependents[next])
            {
                if (--inDegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != workflows.Count)
        {
            throw new InvalidOperationException("A cycle was detected in the workflows' dependsOn relationships; the workflows cannot be ordered.");
        }

        return ordered;
    }

    private static string BuildWorkflowOrderSource(string workflowsNamespace, IReadOnlyList<string> orderedWorkflowIds)
    {
        var writer = new System.Text.StringBuilder();
        writer.AppendLine("// <auto-generated>");
        writer.AppendLine("// This code was generated by the Corvus.Text.Json Arazzo workflow generator.");
        writer.AppendLine("// Do not edit this file directly.");
        writer.AppendLine("// </auto-generated>");
        writer.AppendLine();
        writer.AppendLine("#nullable enable");
        writer.AppendLine();
        writer.Append("namespace ").Append(workflowsNamespace).AppendLine(";");
        writer.AppendLine();
        writer.AppendLine("/// <summary>Workflow-level dependsOn ordering for the document's workflows.</summary>");
        writer.AppendLine("public static class WorkflowOrder");
        writer.AppendLine("{");
        writer.AppendLine("    /// <summary>The document's workflow ids in an order that satisfies their workflow-level <c>dependsOn</c> declarations. Run them in this order (each workflow's dependencies precede it).</summary>");
        writer.Append("    public static System.Collections.Generic.IReadOnlyList<string> ExecutionOrder { get; } = [")
            .Append(string.Join(", ", orderedWorkflowIds.Select(EmitText.Quote))).AppendLine("];");
        writer.AppendLine("}");
        return writer.ToString();
    }
}

/// <summary>
/// Options controlling Arazzo workflow generation.
/// </summary>
/// <param name="RootNamespace">The root .NET namespace (executors go under <c>&lt;RootNamespace&gt;.Workflows</c>, inputs models under <c>&lt;RootNamespace&gt;.Models.&lt;Workflow&gt;</c>).</param>
/// <param name="OutputsTypeName">The fully-qualified type of the workflow outputs (defaults to <see cref="JsonElement"/>).</param>
/// <param name="Durable">When <see langword="true"/>, emit durable executors (checkpoint &amp; resume capable, returning <c>WorkflowRunResult&lt;TOutputs&gt;</c>) instead of straight-line ones.</param>
/// <param name="SubWorkflowSourceNamespaces">
/// Map of <c>arazzo</c>-type <c>sourceDescriptions</c> name → the .NET namespace that source's workflow
/// executors were generated into, used to resolve cross-document sub-workflow targets
/// (<c>$sourceDescriptions.&lt;name&gt;.&lt;workflowId&gt;</c>). <see langword="null"/> when the document has
/// no cross-document sub-workflow references.
/// </param>
public readonly record struct ArazzoGenerationOptions(
    string RootNamespace,
    string OutputsTypeName = "Corvus.Text.Json.JsonElement",
    bool Durable = false,
    IReadOnlyDictionary<string, string>? SubWorkflowSourceNamespaces = null);