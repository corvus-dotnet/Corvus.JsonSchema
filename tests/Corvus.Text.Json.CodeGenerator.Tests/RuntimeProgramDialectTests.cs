// <copyright file="RuntimeProgramDialectTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The dialect an emitted program compiles its documents with follows the fallback vocabulary. The OpenAPI 2.0
/// and 3.0 Schema Object vocabularies share the draft 4 URI, so the URI alone would make them draft 4, which has
/// neither <c>nullable</c> nor the formats OpenAPI adds.
/// </summary>
[TestClass]
public class RuntimeProgramDialectTests
{
    [TestMethod]
    public void OpenApi30VocabularyIsTheOpenApi30Dialect()
    {
        Assert.AreEqual("OpenApi30", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary));
    }

    [TestMethod]
    public void OpenApi20VocabularyIsTheOpenApi20Dialect()
    {
        Assert.AreEqual("OpenApi20", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.OpenApi20.VocabularyAnalyser.DefaultVocabulary));
    }

    [TestMethod]
    public void JsonSchemaVocabulariesAreTheirDrafts()
    {
        Assert.AreEqual("Draft4", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary));
        Assert.AreEqual("Draft6", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary));
        Assert.AreEqual("Draft7", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary));
        Assert.AreEqual("Draft201909", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary));
        Assert.AreEqual("Draft202012", RuntimeProgramGenerator.DialectFor(global::Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary));
    }
}