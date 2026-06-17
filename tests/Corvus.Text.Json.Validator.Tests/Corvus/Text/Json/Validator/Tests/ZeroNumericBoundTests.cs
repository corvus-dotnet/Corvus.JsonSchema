// <copyright file="ZeroNumericBoundTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Regression tests for issue #819: a <c>number</c>-typed schema with a zero-valued
/// numeric bound (<c>minimum</c>/<c>exclusiveMinimum</c>/<c>maximum</c> = <c>0</c>)
/// mis-validated values whose absolute value lies in the open interval <c>(0, 0.1)</c>.
/// </summary>
[TestClass]
public class ZeroNumericBoundTests
{
    private static readonly JsonSchema MinimumZero = JsonSchema.FromText(
        """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "number", "minimum": 0 }""",
        "https://example.com/test/819/minimum-zero");

    private static readonly JsonSchema ExclusiveMinimumZero = JsonSchema.FromText(
        """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "number", "exclusiveMinimum": 0 }""",
        "https://example.com/test/819/exclusive-minimum-zero");

    private static readonly JsonSchema MaximumZero = JsonSchema.FromText(
        """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "number", "maximum": 0 }""",
        "https://example.com/test/819/maximum-zero");

    private static readonly JsonSchema ExclusiveMaximumZero = JsonSchema.FromText(
        """{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "number", "exclusiveMaximum": 0 }""",
        "https://example.com/test/819/exclusive-maximum-zero");

    [TestMethod]
    [DataRow("0", true)]
    [DataRow("0.05", true)]
    [DataRow("0.083", true)]
    [DataRow("0.1", true)]
    [DataRow("0.5", true)]
    [DataRow("1", true)]
    [DataRow("-0.05", false)]
    [DataRow("-0.083", false)]
    [DataRow("-1", false)]
    public void MinimumZero_Number(string json, bool expected)
    {
        Assert.AreEqual(expected, MinimumZero.Validate(json));
    }

    [TestMethod]
    [DataRow("0", false)]
    [DataRow("0.05", true)]
    [DataRow("0.083", true)]
    [DataRow("0.1", true)]
    [DataRow("0.5", true)]
    [DataRow("1", true)]
    [DataRow("-0.05", false)]
    [DataRow("-1", false)]
    public void ExclusiveMinimumZero_Number(string json, bool expected)
    {
        Assert.AreEqual(expected, ExclusiveMinimumZero.Validate(json));
    }

    [TestMethod]
    [DataRow("0", true)]
    [DataRow("0.05", false)]
    [DataRow("0.083", false)]
    [DataRow("0.1", false)]
    [DataRow("0.5", false)]
    [DataRow("1", false)]
    [DataRow("-0.05", true)]
    [DataRow("-0.083", true)]
    [DataRow("-1", true)]
    public void MaximumZero_Number(string json, bool expected)
    {
        Assert.AreEqual(expected, MaximumZero.Validate(json));
    }

    [TestMethod]
    [DataRow("0", false)]
    [DataRow("0.05", false)]
    [DataRow("0.083", false)]
    [DataRow("0.1", false)]
    [DataRow("0.5", false)]
    [DataRow("1", false)]
    [DataRow("-0.05", true)]
    [DataRow("-0.083", true)]
    [DataRow("-1", true)]
    public void ExclusiveMaximumZero_Number(string json, bool expected)
    {
        Assert.AreEqual(expected, ExclusiveMaximumZero.Validate(json));
    }
}