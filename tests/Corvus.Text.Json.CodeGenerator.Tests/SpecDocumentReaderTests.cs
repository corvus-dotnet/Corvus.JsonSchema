// <copyright file="SpecDocumentReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The specification reader hands JSON through untouched, converts YAML, and builds the document resolver with or
/// without a resolver to consult first.
/// </summary>
[TestClass]
public class SpecDocumentReaderTests
{
    [TestMethod]
    public void ReadAsJson_HandsJsonThroughAndConvertsYaml()
    {
        string directory = CodeGeneratorRunner.CreateTempOutputDirectory();
        try
        {
            string json = Path.Combine(directory, "spec.json");
            File.WriteAllText(json, "{\"openapi\":\"3.0.0\"}");
            string yaml = Path.Combine(directory, "spec.yaml");
            File.WriteAllText(yaml, "openapi: 3.0.0\n");

            CollectionAssert.AreEqual(File.ReadAllBytes(json), SpecDocumentReader.ReadAsJson(json, useYaml: false));
            StringAssert.Contains(Encoding.UTF8.GetString(SpecDocumentReader.ReadAsJson(yaml, useYaml: true)), "\"openapi\"");
        }
        finally
        {
            CodeGeneratorRunner.CleanupTempDirectory(directory);
        }
    }

    [TestMethod]
    public void CreateDocumentResolver_WorksWithAndWithoutAResolverToConsultFirst()
    {
        using CompoundDocumentResolver withoutFirst = SpecDocumentReader.CreateDocumentResolver(useYaml: false);
        using FileSystemDocumentResolver first = new();
        using CompoundDocumentResolver withFirst = SpecDocumentReader.CreateDocumentResolver(useYaml: true, first);

        Assert.IsNotNull(withoutFirst);
        Assert.IsNotNull(withFirst);
    }
}