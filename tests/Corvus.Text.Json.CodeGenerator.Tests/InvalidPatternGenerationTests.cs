// <copyright file="InvalidPatternGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The generator stops at a schema whose <c>pattern</c> or <c>patternProperties</c> pattern is not a valid ECMA-262
/// regular expression, with or without a program image, as the runtime evaluator refuses to compile such a schema.
/// </summary>
[TestClass]
public class InvalidPatternGenerationTests
{
    [TestMethod]
    [DataRow("""{"type": "string", "pattern": "a{2,1}"}""", "a{2,1}", "pattern", false)]
    [DataRow("""{"type": "string", "pattern": "a{2,1}"}""", "a{2,1}", "pattern", true)]
    [DataRow("""{"type": "object", "patternProperties": {"^(?<n>a)(?<n>b)": {"type": "integer"}}}""", "^(?<n>a)(?<n>b)", "patternProperties", false)]
    [DataRow("""{"type": "object", "patternProperties": {"^(?<n>a)(?<n>b)": {"type": "integer"}}}""", "^(?<n>a)(?<n>b)", "patternProperties", true)]
    [DataRow("""{"type": "object", "patternProperties": {"^.{5,2}$": {"type": "integer"}}}""", "^.{5,2}$", "patternProperties", false)]
    public async Task GenerateCode_ForASchemaWithAnInvalidPattern_StopsAndNamesThePattern(string schema, string pattern, string keyword, bool withProgramImage)
    {
        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => GenerateAsync(schema, withProgramImage));
        StringAssert.StartsWith(thrown.Message, $"The pattern '{pattern}' of the keyword '{keyword}' is not a valid ECMA-262 regular expression. ");
    }

    [TestMethod]
    public async Task GenerateCode_ForASchemaWithAnInvalidPattern_SaysWhatIsWrongAndWhere()
    {
        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => GenerateAsync("""{"type": "string", "pattern": "^[a-z]{3,1}$"}""", withProgramImage: true));
        Assert.AreEqual(
            "The pattern '^[a-z]{3,1}$' of the keyword 'pattern' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 11.",
            thrown.Message);
    }

    private static async Task<IReadOnlyList<GeneratedCodeFile>> GenerateAsync(string schema, bool withProgramImage)
    {
        string path = Path.Combine(Path.GetTempPath(), $"invalid-pattern-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, schema);
        using CompoundDocumentResolver resolver = new(new FileSystemDocumentResolver());
        try
        {
            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);
            JsonSchemaTypeBuilder typeBuilder = new(resolver, vocabularyRegistry);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                new JsonReference(path),
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            CSharpLanguageProvider.Options options = new(
                "TestGenerated",
                namedTypes: [new CSharpLanguageProvider.NamedType(rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location, "Root")],
                programCompiler: withProgramImage ? Corvus.Json.CodeGenerator.RuntimeProgramCompiler.Compile : null);
            CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
            return [.. typeBuilder.GenerateCodeUsing(languageProvider, [rootType], CancellationToken.None).OrderBy(f => f.FileName, StringComparer.Ordinal)];
        }
        finally
        {
            File.Delete(path);
        }
    }
}