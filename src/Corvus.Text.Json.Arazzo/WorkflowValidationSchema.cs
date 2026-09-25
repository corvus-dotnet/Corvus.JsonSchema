// <copyright file="WorkflowValidationSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Builds the JSON Schema document a workflow's inputs (or any sub-schema of a workflow or source document) are
/// validated against: the sub-schema placed under a known <c>$defs</c> member and referenced from the root, with the
/// <c>$defs</c>, <c>components</c> and <c>definitions</c> of the documents it may refer to carried alongside, so a
/// <c>$ref</c> inside it resolves without the whole document. One definition, because the control plane validates a
/// clear start against it and a runner validates a sealed start's inputs against it at first claim (ADR 0065
/// decision 9), and two builders would be two schemas that could disagree about what a run admits.
/// </summary>
public static class WorkflowValidationSchema
{
    /// <summary>The <c>$defs</c> member a built validation schema places its target sub-schema under.</summary>
    public const string TargetName = "__corvusTarget";

    /// <summary>The same-document reference to the target sub-schema (a <c>$defs</c> member, so a known schema location).</summary>
    public const string TargetRef = "#/$defs/" + TargetName;

    /// <summary>
    /// Writes the validation schema for a workflow's declared <c>inputs</c>: the inline JSON Schema at
    /// <c>workflows[workflowId].inputs</c>, wrapped with the Arazzo document's reusable schema objects.
    /// </summary>
    /// <param name="writer">The writer the schema document is written to.</param>
    /// <param name="workflowRoot">The Arazzo document.</param>
    /// <param name="workflowId">The workflow's id within the document.</param>
    /// <returns><see langword="false"/> when the document has no such workflow or the workflow declares no object inputs schema; nothing is then written.</returns>
    public static bool TryWriteInputs(Utf8JsonWriter writer, in JsonElement workflowRoot, string workflowId)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(workflowId);
        if (!TryFindWorkflow(workflowRoot, workflowId, out JsonElement workflow)
            || !workflow.TryGetProperty("inputs"u8, out JsonElement inputs)
            || inputs.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        WriteWrapper(writer, inputs, [workflowRoot]);
        return true;
    }

    /// <summary>
    /// Wraps a sub-schema as a validation schema document: <c>{ "$ref": "#/$defs/target", "$defs": { target, ...
    /// merged $defs }, components?, definitions? }</c>, the reusable objects taken from <paramref name="roots"/> in
    /// order (first writer wins on a name clash).
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="subSchema">The sub-schema to validate against.</param>
    /// <param name="roots">The documents the sub-schema's references may point into.</param>
    public static void WriteWrapper(Utf8JsonWriter writer, in JsonElement subSchema, ReadOnlySpan<JsonElement> roots)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteString("$ref"u8, TargetRef);
        writer.WritePropertyName("$defs"u8);
        writer.WriteStartObject();
        writer.WritePropertyName(TargetName);
        subSchema.WriteTo(writer);
        WriteMergedDefs(writer, roots);
        writer.WriteEndObject();
        WriteCarriedObject(writer, roots, "components");
        WriteCarriedObject(writer, roots, "definitions");
        writer.WriteEndObject();
    }

    private static bool TryFindWorkflow(in JsonElement root, string workflowId, out JsonElement workflow)
    {
        workflow = default;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("workflows"u8, out JsonElement workflows)
            || workflows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement candidate in workflows.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.Object
                && candidate.TryGetProperty("workflowId"u8, out JsonElement id)
                && id.ValueKind == JsonValueKind.String
                && id.ValueEquals(workflowId))
            {
                workflow = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Merges the <c>$defs</c> members of every root into an open <c>$defs</c> object the writer is inside (first writer wins on a name clash; the target name is reserved).</summary>
    /// <param name="writer">The writer, positioned inside the <c>$defs</c> object.</param>
    /// <param name="roots">The documents whose <c>$defs</c> are carried.</param>
    public static void WriteMergedDefs(Utf8JsonWriter writer, ReadOnlySpan<JsonElement> roots)
    {
        var written = new HashSet<string>(StringComparer.Ordinal) { TargetName };
        foreach (JsonElement root in roots)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("$defs"u8, out JsonElement defs)
                && defs.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> entry in defs.EnumerateObject())
                {
                    if (written.Add(entry.Name))
                    {
                        writer.WritePropertyName(entry.Name);
                        entry.Value.WriteTo(writer);
                    }
                }
            }
        }
    }

    /// <summary>Copies a reusable keyword object (<c>components</c>, <c>definitions</c>) from the first root that has it, as a member of the object the writer is inside.</summary>
    /// <param name="writer">The writer, positioned inside the wrapper object.</param>
    /// <param name="roots">The documents the object is taken from.</param>
    /// <param name="keyword">The keyword.</param>
    public static void WriteCarriedObject(Utf8JsonWriter writer, ReadOnlySpan<JsonElement> roots, string keyword)
    {
        foreach (JsonElement root in roots)
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(keyword, out JsonElement value)
                && value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName(keyword);
                value.WriteTo(writer);
                return;
            }
        }
    }
}