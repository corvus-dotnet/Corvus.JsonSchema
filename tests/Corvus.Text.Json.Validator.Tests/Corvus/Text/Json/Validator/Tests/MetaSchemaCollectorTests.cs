// <copyright file="MetaSchemaCollectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Regression tests for validating an instance against the JSON Schema 2020-12 meta-schema (reached
/// through a <c>$ref</c>) with a results collector attached.
/// </summary>
/// <remarks>
/// <para>
/// This is the shape the Arazzo control plane uses to meta-validate an embedded <c>workflow.inputs</c>
/// schema (validation pass 4): compile <c>{"$ref":"https://json-schema.org/draft/2020-12/schema"}</c>
/// and validate the authored inputs schema against it, collecting positioned findings.
/// </para>
/// <para>
/// When the schema is the recursive 2020-12 meta-schema (<c>$dynamicRef</c>/<c>$dynamicAnchor</c>/deep
/// <c>$defs</c>) and a results collector records per-context results, the generated evaluator commits
/// its child contexts out of order, tripping the collector's LIFO invariant
/// (<c>Debug.Assert</c> "A context has been completed out-of-order", which terminates the process in a
/// DEBUG build; in RELEASE it silently produces corrupt results). The probes below localise the
/// trigger: it is the meta-schema + collector combination — NOT the presence of a root-level
/// <c>$ref</c> (a local <c>$ref</c> is fine), NOT sub-element-ness (the root fails too), and NOT the
/// collector level (Basic and Detailed both fail). The bool-only validation path is sound.
/// </para>
/// </remarks>
[TestClass]
public class MetaSchemaCollectorTests
{
    // The exact schema the control plane compiles for inputs meta-validation: a $ref to the 2020-12
    // meta-schema, which the validator resolves from its embedded cache.
    private static readonly JsonSchema MetaSchema = JsonSchema.FromText(
        """{"$ref":"https://json-schema.org/draft/2020-12/schema"}""",
        "https://example.com/test/inputs-meta-2020-12");

    // A local $ref that resolves within the same document (NOT the recursive meta-schema) — the probe
    // that separates "root-level $ref + collector" from "the recursive meta-schema + collector".
    private static readonly JsonSchema LocalRefSchema = JsonSchema.FromText(
        """{"$ref":"#/$defs/inner","$defs":{"inner":{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}}}""",
        "https://example.com/test/local-ref");

    // A representative inputs schema (the instance being meta-validated).
    private const string InputsSchemaJson = """{"type":"object","properties":{"orderId":{"type":"string"}}}""";

    [TestMethod]
    public void LocalRef_Root_DetailedCollector_Validates()
    {
        // Probe: a root-level $ref to a LOCAL definition (not the meta-schema) with a Detailed collector
        // is fine — so a root $ref per se is not the problem; the meta-schema's recursion is implicated.
        JsonElement root = JsonElement.ParseValue("""{"name":"Alice"}"""u8.ToArray());

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = LocalRefSchema.Validate(in root, collector);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void MetaSchema_Root_NoCollector_Validates()
    {
        // Probe: the meta-schema WITHOUT a collector. The assert lives in the collector, so this
        // isolates that validation itself (bool result) is sound and only the collector path breaks.
        JsonElement root = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(InputsSchemaJson));

        bool valid = MetaSchema.Validate(in root);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void MetaSchema_Root_BasicCollector_Validates()
    {
        // Probe: the meta-schema with a BASIC collector — still fails, so it is not specific to the
        // richer Detailed level; any collector that tracks contexts trips the invariant.
        JsonElement root = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(InputsSchemaJson));

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        bool valid = MetaSchema.Validate(in root, collector);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void MetaSchema_Root_DetailedCollector_Validates()
    {
        // The headline defect: the inputs schema meta-validated at the ROOT with a Detailed collector.
        JsonElement root = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(InputsSchemaJson));

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = MetaSchema.Validate(in root, collector);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void MetaSchema_SubElement_DetailedCollector_Validates()
    {
        // The same schema nested one level down (non-zero element index) — the exact shape of
        // meta-validating workflow.inputs in place inside an Arazzo document.
        JsonElement document = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes($$"""{"inputs":{{InputsSchemaJson}}}"""));
        JsonElement subElement = document.GetProperty("inputs"u8);

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = MetaSchema.Validate(in subElement, collector);

        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void MetaSchema_Root_DetailedCollector_ReportsFailurePositionally()
    {
        // A nonsense inputs schema ("type": 123) must yield a clean non-match positioned at the failing
        // keyword, not a corrupted collector.
        JsonElement root = JsonElement.ParseValue("""{"type":123}"""u8.ToArray());

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = MetaSchema.Validate(in root, collector);

        Assert.IsFalse(valid);

        bool sawFailure = false;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (!result.IsMatch)
            {
                sawFailure = true;
                break;
            }
        }

        Assert.IsTrue(sawFailure);
    }
}
