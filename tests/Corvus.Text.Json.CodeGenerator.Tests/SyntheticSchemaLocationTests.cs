// <copyright file="SyntheticSchemaLocationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// A schema generated from a fragment of a document gets a synthetic location: a <c>$ref</c> document for a fragment
/// root, or the rebased island itself. Those locations reach the generated program and, for a rebased island, the
/// <c>SchemaDocument</c> constants, so they must be the same in every generation.
/// </summary>
[TestClass]
public class SyntheticSchemaLocationTests
{
    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$defs": {
            "alpha": {
              "type": "object",
              "properties": {
                "name": { "type": "string" },
                "next": { "$ref": "#/$defs/alpha" }
              }
            },
            "beta": {
              "type": "object",
              "properties": {
                "count": { "type": "integer" }
              }
            },
            "island": {
              "type": "object",
              "properties": {
                "label": { "type": "string" },
                "child": { "$ref": "#" }
              }
            }
          }
        }
        """;

    // The program's entry point for the synthetic $ref root: the first virtual resource of the builder's registry.
    private const string ExpectedFragmentLines =
        """
        CorvusJsonSchemaProgram.cs: "corvus-schema:///00000001.virtual/Schema#",
        """;

    // The rebased island's own entry point and its string property's, and the island root type's SchemaDocument constants.
    private const string ExpectedRebasedLines =
        """
        CorvusJsonSchemaProgram.cs: "corvus-schema:///00000001.virtual/Schema#/properties/label",
        CorvusJsonSchemaProgram.cs: "corvus-schema:///00000001.virtual/Schema#",
        Root.JsonSchema.cs: public const string SchemaDocument = "00000001.virtual/Schema";
        Root.JsonSchema.cs: public static ReadOnlySpan<byte> SchemaDocumentUtf8 => "00000001.virtual/Schema"u8;
        """;

    // Each builder has its own registry, so each numbers its virtual resources from 1.
    private const string ExpectedSharedLines = ExpectedFragmentLines;

    [TestMethod]
    public async Task FragmentRoot_GeneratedTwice_ProducesIdenticalFiles()
    {
        using TemporarySchema schema = TemporarySchema.Create(Schema);

        IReadOnlyList<GeneratedCodeFile> first = await GenerateAsync(schema.Path, "#/$defs/alpha", rebaseAsRoot: false);
        IReadOnlyList<GeneratedCodeFile> second = await GenerateAsync(schema.Path, "#/$defs/alpha", rebaseAsRoot: false);

        AssertSameFiles(first, second);
        AssertLines(ExpectedFragmentLines, SyntheticLocationLines(first));
    }

    [TestMethod]
    public async Task RebasedIsland_GeneratedTwice_ProducesIdenticalFiles()
    {
        using TemporarySchema schema = TemporarySchema.Create(Schema);

        IReadOnlyList<GeneratedCodeFile> first = await GenerateAsync(schema.Path, "#/$defs/island", rebaseAsRoot: true);
        IReadOnlyList<GeneratedCodeFile> second = await GenerateAsync(schema.Path, "#/$defs/island", rebaseAsRoot: true);

        AssertSameFiles(first, second);
        AssertLines(ExpectedRebasedLines, SyntheticLocationLines(first));
    }

    [TestMethod]
    public async Task FragmentRootAndRebasedIsland_Registration_AddsNoDocumentToTheResolver()
    {
        using TemporarySchema schema = TemporarySchema.Create(Schema);
        using CountingDocumentResolver resolver = new(new CompoundDocumentResolver(new FileSystemDocumentResolver()));

        await GenerateAsync(schema.Path, "#/$defs/alpha", rebaseAsRoot: false, resolver);
        await GenerateAsync(schema.Path, "#/$defs/beta", rebaseAsRoot: true, resolver);

        Assert.AreEqual(0, resolver.AddedDocuments);
    }

    [TestMethod]
    public async Task SharedResolver_EachBuilder_ResolvesItsOwnSyntheticRoot()
    {
        using TemporarySchema schema = TemporarySchema.Create(Schema);
        using CompoundDocumentResolver resolver = new(new FileSystemDocumentResolver());

        IReadOnlyList<GeneratedCodeFile> alpha = await GenerateAsync(schema.Path, "#/$defs/alpha", rebaseAsRoot: false, resolver);
        IReadOnlyList<GeneratedCodeFile> beta = await GenerateAsync(schema.Path, "#/$defs/beta", rebaseAsRoot: false, resolver);

        AssertLines(ExpectedSharedLines, SyntheticLocationLines(alpha));
        AssertLines(ExpectedSharedLines, SyntheticLocationLines(beta));
    }

    private static async Task<IReadOnlyList<GeneratedCodeFile>> GenerateAsync(string path, string fragment, bool rebaseAsRoot, IDocumentResolver sharedResolver = null)
    {
        IDocumentResolver resolver = sharedResolver ?? new CompoundDocumentResolver(new FileSystemDocumentResolver());
        try
        {
            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);
            JsonSchemaTypeBuilder typeBuilder = new(resolver, vocabularyRegistry);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                new JsonReference(path, fragment),
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
                rebaseAsRoot);

            CSharpLanguageProvider.Options options = new(
                "TestGenerated",
                namedTypes: [new CSharpLanguageProvider.NamedType(rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location, "Root")],
                programCompiler: Corvus.Json.CodeGenerator.RuntimeProgramCompiler.Compile);
            CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
            return [.. typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None).OrderBy(f => f.FileName, StringComparer.Ordinal)];
        }
        finally
        {
            if (sharedResolver is null)
            {
                resolver.Dispose();
            }
        }
    }

    private static void AssertSameFiles(IReadOnlyList<GeneratedCodeFile> expected, IReadOnlyList<GeneratedCodeFile> actual)
    {
        Assert.AreEqual(string.Join("\n", expected.Select(f => f.FileName)), string.Join("\n", actual.Select(f => f.FileName)));
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].FileContent, actual[i].FileContent, expected[i].FileName);
        }
    }

    // An exact comparison whose failure message shows both values in full (Assert.AreEqual shortens long strings). The
    // expectations are raw string literals, which take this file's CRLF line endings; the lines are joined with LF.
    private static void AssertLines(string expected, string actual)
    {
        expected = expected.Replace("\r\n", "\n");
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Assert.Fail($"Expected lines:\n{expected}\nActual lines:\n{actual}");
        }
    }

    // The lines of the generated files that carry a synthetic location, each prefixed with its file name.
    private static string SyntheticLocationLines(IReadOnlyList<GeneratedCodeFile> files)
    {
        return string.Join(
            "\n",
            files
                .SelectMany(f => f.FileContent.Split('\n').Select(line => f.FileName + ": " + line.Trim()))
                .Where(line => line.Contains(".virtual/Schema", StringComparison.Ordinal)));
    }

    private sealed class TemporarySchema : IDisposable
    {
        private TemporarySchema(string directory, string path)
        {
            this.Directory = directory;
            this.Path = path;
        }

        public string Directory { get; }

        public string Path { get; }

        public static TemporarySchema Create(string content)
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "synthetic-location-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "schema.json");
            File.WriteAllText(path, content);
            return new TemporarySchema(directory, path);
        }

        public void Dispose()
        {
            System.IO.Directory.Delete(this.Directory, recursive: true);
        }
    }

    private sealed class CountingDocumentResolver(IDocumentResolver inner) : IDocumentResolver
    {
        public int AddedDocuments { get; private set; }

        public bool AddDocument(string uri, System.Text.Json.JsonDocument document)
        {
            this.AddedDocuments++;
            return inner.AddDocument(uri, document);
        }

        public ValueTask<System.Text.Json.JsonElement?> TryResolve(JsonReference reference) => inner.TryResolve(reference);

        public void Reset() => inner.Reset();

        public void Dispose() => inner.Dispose();
    }
}