// <copyright file="CollectorRollbackTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Regression test for a results-collector rollback bug. When a child evaluation context is discarded after
/// producing no results, the collector rolled the result buffer and committed-result stack back to
/// <c>(0, 0)</c>, clobbering earlier committed results and faulting <c>ReadResult</c> when the detailed
/// results were later enumerated. It takes a deeply-branching schema (nested <c>oneOf</c>/<c>if</c>/
/// <c>dependentSchemas</c> over a recursive <c>$dynamicRef</c>) to trigger it; the bundled OpenAPI 3.1
/// metaschema and a real specification (which validates successfully) is the minimal reproduction. Enumerating the
/// detailed results must not fault.
/// </summary>
[TestClass]
public class CollectorRollbackTests
{
    [TestMethod]
    public void OpenApi31DetailedResultsEnumerateWithoutFaulting()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "CollectorRollback", "openapi-3.1.json");
        string instancePath = Path.Combine(AppContext.BaseDirectory, "Schemas", "CollectorRollback", "openapi-instance.json");

        JsonSchema schema = JsonSchema.FromFile(schemaPath);
        byte[] instance = File.ReadAllBytes(instancePath);

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = schema.Validate(new ReadOnlyMemory<byte>(instance), collector);

        Assert.IsTrue(valid, "The bundled specification validates against the OpenAPI 3.1 metaschema.");

        // Walking each result's length headers faulted with an out-of-range index before the rollback fix.
        int resultCount = 0;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            resultCount++;
        }

        Assert.IsTrue(resultCount > 0, "Detailed results must be enumerable without faulting.");
    }
}