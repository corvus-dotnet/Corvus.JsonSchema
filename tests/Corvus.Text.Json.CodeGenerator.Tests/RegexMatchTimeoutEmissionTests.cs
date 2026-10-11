// <copyright file="RegexMatchTimeoutEmissionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Every regular expression in generated code has a match timeout of 1 second: the expressions of the generated
/// types, in each of their forms, and the expressions of the schema evaluation program. There are patterns on which a
/// match in .NET's engine never finishes, and with the timeout the generated code throws
/// <see cref="RegexMatchTimeoutException"/> where it used not to return.
/// </summary>
[TestClass]
public class RegexMatchTimeoutEmissionTests
{
    // Neither pattern is one the emitter or the evaluator matches without a regular expression. On a long run of "a"
    // that does not end the way the pattern needs, each backtracks through every way to divide the run into "a" and
    // "aa". (A group is translated without capture, and .NET then runs ^(a+)+$ as ^a+$, which does not backtrack.)
    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "code": { "type": "string", "pattern": "^(a|aa)+$" }
          },
          "patternProperties": {
            "^(a|aa)+-$": { "type": "integer" }
          }
        }
        """;

    [TestMethod]
    public async Task GeneratedTypes_EveryRegularExpressionForm_HasTheTimeout()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync(withProgramImage: true);

        string[] lines = RegexLines(files.Where(f => f.FileName != "CorvusJsonSchemaProgram.cs"));

        // The [GeneratedRegex] form and the constructed form, as the two sides of one #if. A generated type holds an
        // expression for a patternProperties pattern, which its property helpers match with. It holds none for the
        // pattern keyword, which only the program matches.
        string[] expected =
        [
            """Root.JsonSchema.cs: [GeneratedRegex("^(?:a|aa)+-\\z", RegexOptions.None, matchTimeoutMilliseconds: 1000)]""",
            """Root.JsonSchema.cs: private static Regex CreatePatternProperties() => new("^(?:a|aa)+-\\z", RegexOptions.Compiled, global::System.TimeSpan.FromMilliseconds(1000));""",
        ];

        AssertLines(expected, lines);
    }

    [TestMethod]
    public async Task Program_WithAnImage_HasTheTimeoutOnItsGeneratedRegexesAndInItsOptions()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync(withProgramImage: true);

        string[] lines = RegexLines(files.Where(f => f.FileName == "CorvusJsonSchemaProgram.cs"));

        // The options carry it for the expressions the evaluator constructs where there is no regex generator.
        string[] expected =
        [
            """CorvusJsonSchemaProgram.cs: RegexMatchTimeout = global::System.TimeSpan.FromMilliseconds(1000),""",
            """CorvusJsonSchemaProgram.cs: [GeneratedRegex("^(?:a|aa)+-\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]""",
            """CorvusJsonSchemaProgram.cs: [GeneratedRegex("^(?:a|aa)+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]""",
        ];

        AssertLines(expected, lines);
    }

    [TestMethod]
    public async Task Program_WithNoImage_HasTheTimeoutInItsOptions()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync(withProgramImage: false);

        string[] lines = RegexLines(files.Where(f => f.FileName == "CorvusJsonSchemaProgram.cs"));

        string[] expected =
        [
            """CorvusJsonSchemaProgram.cs: RegexMatchTimeout = global::System.TimeSpan.FromMilliseconds(1000),""",
        ];

        AssertLines(expected, lines);
    }

    /// <summary>
    /// The generated code is compiled and run. Its timeout is fixed at 1 second, so one evaluation here waits for
    /// that long: validation of an instance whose property does not finish its match. The expression a generated type
    /// holds is not run to its timeout. It is checked for the timeout it was constructed with.
    /// </summary>
    [TestMethod]
    public async Task GeneratedCode_OnAMatchThatDoesNotFinish_ThrowsAtTheTimeout()
    {
        IReadOnlyList<GeneratedCodeFile> files = await GenerateAsync(withProgramImage: true);

        var context = new AssemblyLoadContext(nameof(RegexMatchTimeoutEmissionTests), isCollectible: true);
        try
        {
            Assembly assembly = CompileAndLoad(files, context);

            Regex[] expressions =
            [
                .. assembly.GetTypes()
                    .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    .Where(f => f.FieldType == typeof(Regex))
                    .Select(f => (Regex)f.GetValue(null)!)
            ];

            Assert.AreEqual(1, expressions.Length, "The expression of the pattern property.");
            foreach (Regex expression in expressions)
            {
                Assert.AreEqual(TimeSpan.FromSeconds(1), expression.MatchTimeout, expression.ToString());
            }

            Func<string, bool> evaluate = assembly.GetType("TestGenerated.Runner", throwOnError: true)!
                .GetMethod("Evaluate", BindingFlags.Static | BindingFlags.Public)!
                .CreateDelegate<Func<string, bool>>();

            Assert.IsTrue(evaluate("""{"code": "aaa", "aaa-": 1}"""));
            Assert.IsFalse(evaluate("""{"code": "aab"}"""));

            // The worker is a background thread. If the match has no timeout it is left behind, and the test fails
            // at the end of the wait instead of never ending.
            string slow = "{\"code\": \"" + new string('a', 48) + "#\"}";
            Exception thrown = null;
            bool returned = false;
            var worker = new Thread(() =>
            {
                try
                {
                    evaluate(slow);
                    returned = true;
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            })
            {
                IsBackground = true,
            };

            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            worker.Start();
            if (!worker.Join(TimeSpan.FromSeconds(30)))
            {
                Assert.Fail("The generated code had not finished after 30 seconds. The match has no timeout.");
            }

            Assert.IsFalse(returned, "The generated code returned an answer. The match was expected not to finish.");
            Assert.IsInstanceOfType<RegexMatchTimeoutException>(thrown);
            Assert.IsTrue(elapsed.Elapsed >= TimeSpan.FromMilliseconds(900), $"The match stopped after {elapsed.Elapsed}, before the timeout.");
        }
        finally
        {
            context.Unload();
        }
    }

    private static async Task<IReadOnlyList<GeneratedCodeFile>> GenerateAsync(bool withProgramImage)
    {
        string path = Path.Combine(Path.GetTempPath(), $"regex-timeout-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, Schema);
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

    // The lines of generated code that construct a regular expression or set the evaluator's timeout.
    private static string[] RegexLines(IEnumerable<GeneratedCodeFile> files)
    {
        return
        [
            .. files.SelectMany(f => f.FileContent.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Contains("GeneratedRegex(", StringComparison.Ordinal) || l.Contains("Regex Create", StringComparison.Ordinal) && l.Contains("=> new(", StringComparison.Ordinal) || l.Contains("RegexMatchTimeout", StringComparison.Ordinal))
                .Select(l => $"{f.FileName}: {l}"))
        ];
    }

    private static void AssertLines(string[] expected, string[] actual)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", actual)}");
        }
    }

    /// <summary>
    /// Compiles the generated files with <c>DYNAMIC_BUILD</c> defined, as a build with no regex source generator has
    /// it: the types construct their expressions, and the program hands none to the evaluator, which constructs its
    /// own with the options the program gives it.
    /// </summary>
    private static Assembly CompileAndLoad(IReadOnlyList<GeneratedCodeFile> files, AssemblyLoadContext context)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(GeneratedDocumentationTests.ReadCompilationDefines().Append("DYNAMIC_BUILD").ToArray());

        List<SyntaxTree> trees = [.. files.Select(f => CSharpSyntaxTree.ParseText(f.FileContent, parseOptions, path: f.FileName))];
        trees.Add(CSharpSyntaxTree.ParseText(
            """
            global using global::System;
            global using global::System.Collections.Generic;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;

            namespace TestGenerated;

            public static class Runner
            {
                public static bool Evaluate(string json)
                {
                    using var document = global::Corvus.Text.Json.ParsedJsonDocument<Root>.Parse(global::System.Text.Encoding.UTF8.GetBytes(json));
                    return document.RootElement.EvaluateSchema();
                }
            }
            """,
            parseOptions,
            path: "Runner.cs"));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "RegexMatchTimeoutGenerated",
            trees,
            GeneratedDocumentationTests.BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(true));

        using var image = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(image);
        if (!result.Success)
        {
            Assert.Fail($"The generated code did not compile:\n{string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(25))}");
        }

        image.Position = 0;
        return context.LoadFromStream(image);
    }
}