// <copyright file="RuntimeProgramKeyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The keys an emitted program uses for its documents carry no build-machine path: file documents are keyed
/// relative to their common directory, and the reference-only wrapper documents the type builder synthesises are
/// rewritten to those keys.
/// </summary>
[TestClass]
public class RuntimeProgramKeyTests
{
    [TestMethod]
    public void FileDocumentsAreKeyedRelativeToTheirCommonDirectory()
    {
        IReadOnlyDictionary<string, string> keys = RuntimeProgramGenerator.MapDocumentKeys(
        [
            "/home/build/project/schemas/person.json",
            "file:///home/build/project/schemas/shared/address.json#",
            "/home/build/project/other.json",
            "d41d8cd9-8f00-b204-e980-0998ecf8427e/Schema",
            "https://example.com/schemas/remote#",
        ]);

        Assert.AreEqual("corvus-schema:///schemas/person.json", keys["/home/build/project/schemas/person.json"]);
        Assert.AreEqual("corvus-schema:///schemas/shared/address.json", keys["file:///home/build/project/schemas/shared/address.json#"]);
        Assert.AreEqual("corvus-schema:///other.json", keys["/home/build/project/other.json"]);
        Assert.AreEqual("corvus-schema:///d41d8cd9-8f00-b204-e980-0998ecf8427e/Schema", keys["d41d8cd9-8f00-b204-e980-0998ecf8427e/Schema"], "A bare path is already machine-independent.");
        Assert.AreEqual("https://example.com/schemas/remote", keys["https://example.com/schemas/remote#"], "An absolute $id is kept, without its empty fragment.");
    }

    [TestMethod]
    public void ASingleFileDocumentIsKeyedByItsName()
    {
        IReadOnlyDictionary<string, string> keys = RuntimeProgramGenerator.MapDocumentKeys(["/home/build/project/schemas/person.json"]);
        Assert.AreEqual("corvus-schema:///person.json", keys["/home/build/project/schemas/person.json"]);
    }

    [TestMethod]
    public void WrapperDocumentsReferToTheProgramKey()
    {
        IReadOnlyDictionary<string, string> keys = RuntimeProgramGenerator.MapDocumentKeys(
        [
            "/home/build/project/schemas/person.json",
            "/home/build/project/schemas/pet.json",
        ]);

        string wrapper = """{"$ref": "/home/build/project/schemas/person.json#/$defs/name"}""";
        Assert.AreEqual("""{"$ref": "corvus-schema:///person.json#/$defs/name"}""", RuntimeProgramGenerator.MapReferenceDocument(wrapper, keys));

        string fileUri = """{"$ref": "file:///home/build/project/schemas/pet.json"}""";
        Assert.AreEqual("""{"$ref": "corvus-schema:///pet.json"}""", RuntimeProgramGenerator.MapReferenceDocument(fileUri, keys));

        string unknown = """{"$ref": "/elsewhere/other.json#/x"}""";
        Assert.AreEqual(unknown, RuntimeProgramGenerator.MapReferenceDocument(unknown, keys), "A reference to a document outside the program is left alone.");

        string schema = """{"$ref": "/home/build/project/schemas/person.json", "type": "object"}""";
        Assert.AreEqual(schema, RuntimeProgramGenerator.MapReferenceDocument(schema, keys), "Only reference-only wrappers are rewritten.");
    }
}
