// <copyright file="DependentSchemasOrderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Regression test: a property reachable only through a <c>dependentSchemas</c> subschema must be credited for
/// <c>unevaluatedProperties</c> regardless of its position relative to the property that triggers the dependency.
/// <c>dependentSchemas</c> is an applicator over the whole object, so it must be evaluated before the property
/// loop that checks <c>unevaluatedProperties</c> inline. Reproduces the OpenAPI 3.1 parameter <c>example</c>
/// over-rejection (where real specs place <c>example</c> before <c>schema</c>).
/// </summary>
[TestClass]
public class DependentSchemasOrderTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": { "schema": true },
            "dependentSchemas": { "schema": { "properties": { "example": true } } },
            "unevaluatedProperties": false
        }
        """,
        "https://example.com/test/dependent-schemas-order");

    [TestMethod]
    [DataRow("{\"schema\":1,\"example\":1}")]
    [DataRow("{\"example\":1,\"schema\":1}")]
    public void ExampleCreditedByDependentSchemaRegardlessOfOrder(string json)
    {
        Assert.IsTrue(
            Schema.Validate(json),
            "example is evaluated via dependentSchemas.schema and must be credited for unevaluatedProperties.");
    }
}