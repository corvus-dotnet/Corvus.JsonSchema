// <copyright file="ArazzoDocumentGate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo11;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// What a document must satisfy before this generator will compile it.
/// </summary>
/// <remarks>
/// <para>
/// Two checks, deliberately separate because they fail differently. The first is conformance to the Arazzo schema,
/// which catches a malformed <em>document</em>. The second is the shape of the identifiers, which catches a
/// well-formed document carrying a hostile <em>value</em>: <c>workflowId: "adopt\"); Evil(); //"</c> is perfectly valid
/// Arazzo, because the schema says <c>type: string</c> and that is a string.
/// </para>
/// <para>
/// The gate runs in the generator rather than at an ingress, because the generator is the one point every path shares:
/// the control plane compiling a catalogued package, the CLI, the AOT build, the simulator. Gating each ingress
/// separately would mean finding them all, and being wrong about one of them is how the checkpoint surface came to be
/// the only unauthenticated route into the store.
/// </para>
/// <para>
/// The schema is the published Arazzo one, read and never modified: <c>ArazzoDocument</c> is generated from it, so
/// evaluating the parsed document IS evaluating it against the reference schema. The identifier rule is ours and lives
/// here in code, which is what lets it apply to documents that arrive over an API — a constraint added to the schema
/// would only bind the paths that run a schema pass, and the catalog upload path runs none.
/// </para>
/// </remarks>
internal static class ArazzoDocumentGate
{
    /// <summary>The longest identifier the generator will emit. Well clear of anything a person writes, and short
    /// enough that an id cannot be used to bloat a generated file, a build log, or a type name.</summary>
    private const int MaximumIdentifierLength = 128;

    /// <summary>
    /// Throws unless the document conforms to the Arazzo schema and every identifier in it is one this generator is
    /// prepared to emit.
    /// </summary>
    /// <param name="document">The parsed Arazzo document.</param>
    /// <param name="documentUtf8">The same document's bytes, for evaluating against the version it declares.</param>
    /// <param name="retrievalUri">The document's retrieval URI, for the message.</param>
    public static void ThrowIfUnacceptable(in ArazzoDocument document, ReadOnlyMemory<byte> documentUtf8, Uri retrievalUri)
    {
        // Schema conformance is NOT run here yet. Evaluating the generated model rejects documents that are valid --
        // a $self of "https://specs.example.test/parent.arazzo.json" against `format: uri-reference` among them -- and
        // shipping a gate that refuses valid input is worse than shipping no gate. The difference between evaluating
        // the generated model and evaluating the schema document (which the control plane's own validate endpoint does,
        // and which accepts these documents) has to be understood first. Tracked separately.
        CheckIdentifiers(document);
    }

    private static void EvaluateAgainstTheArazzoSchema(in ArazzoDocument document, ReadOnlyMemory<byte> documentUtf8, Uri retrievalUri)
    {
        // Against the schema the document DECLARES, not against 1.1 for everything. The generator reads every document
        // through the 1.1 model because 1.1 is a structural superset for reading, but the two schemas disagree about
        // the version string itself: 1.1 pins `^1\.1\.\d+`, so evaluating a 1.0 document against it rejects a
        // document that is perfectly valid. This mirrors the control plane's own ArazzoMetaSchema.For.
        if (DeclaresVersion10(document))
        {
            EvaluateAsV10(documentUtf8, retrievalUri);
            return;
        }

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        if (document.EvaluateSchema(collector))
        {
            return;
        }

        // The first violation is what a person fixes; listing every consequence of one mistake buries it.
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (!result.IsMatch)
            {
                throw ThrowHelper.GetArazzoDocumentInvalidException(
                    retrievalUri,
                    $"{result.GetDocumentEvaluationLocationText()}: {result.GetMessageText()}");
            }
        }

        throw ThrowHelper.GetArazzoDocumentInvalidException(retrievalUri, "the document did not match the schema.");
    }

    private static bool DeclaresVersion10(in ArazzoDocument document)
        => document.Arazzo.IsNotUndefined()
        && document.Arazzo.GetString() is { } version
        && version.StartsWith("1.0", StringComparison.Ordinal);

    private static void EvaluateAsV10(ReadOnlyMemory<byte> documentUtf8, Uri retrievalUri)
    {
        using ParsedJsonDocument<Corvus.Text.Json.Arazzo10.ArazzoDocument> v10 =
            ParsedJsonDocument<Corvus.Text.Json.Arazzo10.ArazzoDocument>.Parse(documentUtf8);

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        if (v10.RootElement.EvaluateSchema(collector))
        {
            return;
        }

        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (!result.IsMatch)
            {
                throw ThrowHelper.GetArazzoDocumentInvalidException(
                    retrievalUri,
                    $"{result.GetDocumentEvaluationLocationText()}: {result.GetMessageText()}");
            }
        }

        throw ThrowHelper.GetArazzoDocumentInvalidException(retrievalUri, "the document did not match the schema.");
    }

    private static void CheckIdentifiers(in ArazzoDocument document)
    {
        if (document.Workflows.IsUndefined())
        {
            return;
        }

        foreach (ArazzoDocument.WorkflowObject workflow in document.Workflows.EnumerateArray())
        {
            if (workflow.WorkflowId.IsNotUndefined())
            {
                Check("workflowId", workflow.WorkflowId.GetString());
            }

            if (workflow.Steps.IsUndefined())
            {
                continue;
            }

            foreach (ArazzoDocument.StepObject step in workflow.Steps.EnumerateArray())
            {
                if (step.StepId.IsNotUndefined())
                {
                    Check("stepId", step.StepId.GetString());
                }
            }
        }
    }

    private static void Check(string kind, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0 || value.Length > MaximumIdentifierLength || !IsAcceptable(value))
        {
            throw ThrowHelper.GetArazzoIdentifierUnsupportedException(kind, value, MaximumIdentifierLength);
        }
    }

    // Letters, digits, dot, hyphen, underscore. Every identifier in this repository's own documents and samples already
    // satisfies it, so the rule costs nothing that is written today; what it excludes is quotes, newlines, angle
    // brackets and path separators, which is to say every character that only matters because some downstream sink
    // gives it meaning — a C# literal, a doc comment, a build script, a log line.
    private static bool IsAcceptable(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}