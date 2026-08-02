// <copyright file="GlobalNamespaceModelTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for source generation targeting a struct declared in the global namespace
/// (see https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/906).
/// <see cref="GlobalNamespaceModel"/> is declared without a namespace in the
/// GeneratedModels project; these tests prove that the generator produced a working
/// type for it. A regression to silent non-generation fails this file at compile time.
/// </summary>
[TestClass]
public class GlobalNamespaceModelTests
{
    [TestMethod]
    public void GlobalNamespaceModel_StringForm_EvaluatesSchemaValid()
    {
        using ParsedJsonDocument<GlobalNamespaceModel> doc =
            ParsedJsonDocument<GlobalNamespaceModel>.Parse("\"just a string\"");

        Assert.IsTrue(doc.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GlobalNamespaceModel_ObjectForm_EvaluatesSchemaValid()
    {
        using ParsedJsonDocument<GlobalNamespaceModel> doc =
            ParsedJsonDocument<GlobalNamespaceModel>.Parse("""{"expression": "a + b"}""");

        Assert.IsTrue(doc.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GlobalNamespaceModel_ObjectFormMissingRequiredProperty_EvaluatesSchemaInvalid()
    {
        using ParsedJsonDocument<GlobalNamespaceModel> doc =
            ParsedJsonDocument<GlobalNamespaceModel>.Parse("""{"other": 1}""");

        Assert.IsFalse(doc.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GlobalNamespaceModel_NumberForm_EvaluatesSchemaInvalid()
    {
        using ParsedJsonDocument<GlobalNamespaceModel> doc =
            ParsedJsonDocument<GlobalNamespaceModel>.Parse("42");

        Assert.IsFalse(doc.RootElement.EvaluateSchema());
    }
}