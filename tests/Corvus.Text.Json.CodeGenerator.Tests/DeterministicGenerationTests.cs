// <copyright file="DeterministicGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Generated code must not depend on the machine that generates it: neither on the culture of the generating process
/// nor on that process's string hash seed.
/// </summary>
[TestClass]
public class DeterministicGenerationTests
{
    [TestMethod]
    public async Task GenerateCode_NumericEnumConstant_CompilesUnderSwedishCulture()
    {
        CultureInfo swedish = CultureInfo.GetCultureInfo("sv-SE");
        if (swedish.NumberFormat.NegativeSign != "\u2212")
        {
            Assert.Inconclusive("The sv-SE culture data in this process does not use U+2212 as its negative sign (for example in globalization-invariant mode), so this test cannot show the defect.");
        }

        IReadOnlyCollection<GeneratedCodeFile> files = await GenerateUnderCulture(swedish, """{ "enum": [0.5, 1e-3, 2] }""");

        AssertCompiles(files);

        string[] constants = files
            .SelectMany(f => f.FileContent.Split('\n'))
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("public static readonly NormalizedJsonNumber ", StringComparison.Ordinal))
            .ToArray();

        // 0.5 normalises to an empty integral part, fractional "5" and exponent -1; 1e-3 to exponent -3. Under sv-SE a
        // culture-formatted exponent is written with U+2212, which is not a C# token (CS1056).
        AssertLines(
            [
                """public static readonly NormalizedJsonNumber Enum1 = new(false, [..""u8], [.."5"u8], -1);""",
                """public static readonly NormalizedJsonNumber Enum2 = new(false, [.."1"u8], [..""u8], -3);""",
                """public static readonly NormalizedJsonNumber Enum3 = new(false, [.."2"u8], [..""u8], 0);""",
            ],
            constants);
    }

    [TestMethod]
    public async Task GenerateCode_SharedSimpleTypes_AreEmittedInFirstSeenOrder()
    {
        const string schema =
            """
            {
              "type": "object",
              "properties": {
                "a": { "type": "string", "format": "uuid" },
                "b": { "type": "integer" },
                "c": { "type": "string" },
                "d": { "type": "boolean" },
                "e": { "type": "number" },
                "f": { "type": "string", "format": "date" },
                "g": { "type": "string", "format": "uri" },
                "h": { "type": "string", "format": "email" },
                "i": { "type": "string", "format": "date-time" },
                "j": { "type": "string", "format": "ipv4" },
                "k": { "type": "string", "format": "hostname" },
                "l": { "type": "integer", "format": "int32" },
                "m": { "type": "string" },
                "n": { "type": "integer" }
              }
            }
            """;

        // The order in which the naming pass first sees each shared type: properties in location order.
        string[] expected =
        [
            "JsonUuid.cs", "JsonUuid.Mutable.cs", "JsonUuid.JsonSchema.cs",
            "JsonInteger.cs", "JsonInteger.Mutable.cs", "JsonInteger.JsonSchema.cs",
            "JsonString.cs", "JsonString.Mutable.cs", "JsonString.JsonSchema.cs",
            "JsonBoolean.cs", "JsonBoolean.Mutable.cs", "JsonBoolean.JsonSchema.cs",
            "JsonNumber.cs", "JsonNumber.Mutable.cs", "JsonNumber.JsonSchema.cs",
            "JsonDate.cs", "JsonDate.Mutable.cs", "JsonDate.JsonSchema.cs",
            "JsonUri.cs", "JsonUri.Mutable.cs", "JsonUri.JsonSchema.cs",
            "JsonEmail.cs", "JsonEmail.Mutable.cs", "JsonEmail.JsonSchema.cs",
            "JsonDateTime.cs", "JsonDateTime.Mutable.cs", "JsonDateTime.JsonSchema.cs",
            "JsonIpV4.cs", "JsonIpV4.Mutable.cs", "JsonIpV4.JsonSchema.cs",
            "JsonHostname.cs", "JsonHostname.Mutable.cs", "JsonHostname.JsonSchema.cs",
            "JsonInt32.cs", "JsonInt32.Mutable.cs", "JsonInt32.JsonSchema.cs",
        ];

        // Two generations in one process see the same string hash seed, so they would agree with each other even if
        // the order came from hashing; the comparison with the fixed first-seen order is what a hash order fails.
        AssertLines(expected, GetSharedSimpleTypeFileNames(await InProcessGenerationTests.GenerateInProcessFromContent(schema)));
        AssertLines(expected, GetSharedSimpleTypeFileNames(await InProcessGenerationTests.GenerateInProcessFromContent(schema)));
    }

    [TestMethod]
    public void CodeGenerator_FormatsNumbersWithTheInvariantCulture()
    {
        CultureInfo swedish = CultureInfo.GetCultureInfo("sv-SE");
        if (swedish.NumberFormat.NegativeSign != "\u2212" || swedish.NumberFormat.NumberDecimalSeparator != ",")
        {
            Assert.Inconclusive("The sv-SE culture data in this process does not differ from the invariant culture (for example in globalization-invariant mode), so this test cannot show the defect.");
        }

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = swedish;
        try
        {
            Corvus.Json.CodeGeneration.CodeGenerator generator = new(CSharpLanguageProvider.DefaultWithOptions(new CSharpLanguageProvider.Options(defaultNamespace: "Test")), CancellationToken.None);
            generator
                .Append((sbyte)-1).Append(' ')
                .Append((byte)2).Append(' ')
                .Append((short)-3).Append(' ')
                .Append(-4).Append(' ')
                .Append(-5L).Append(' ')
                .Append(-1.5f).Append(' ')
                .Append(-2.5d).Append(' ')
                .Append(-3.5m).Append(' ')
                .Append((ushort)6).Append(' ')
                .Append(7u).Append(' ')
                .Append(8ul).Append(' ')
                .Append((object)(-9.5)).Append(' ')
                .Append((object)"text").Append('|')
                .Append((object)null).Append('|')
                .AppendFormat("{0}", -1.5).Append(' ')
                .AppendFormat("{0}/{1}", -1, 2.5).Append(' ')
                .AppendFormat("{0}/{1}/{2}", -1, 2.5, -3).Append(' ')
                .AppendFormat("{0}/{1}/{2}/{3}", -1, 2.5, -3, 4.5).Append(' ')
                .AppendFormatIndent("{0}", -1.5).Append(' ')
                .AppendFormatIndent("{0}/{1}", -1, 2.5).Append(' ')
                .AppendFormatIndent("{0}/{1}/{2}", -1, 2.5, -3).Append(' ')
                .AppendFormatIndent("{0}/{1}/{2}/{3}", -1, 2.5, -3, 4.5);

            Assert.AreEqual(
                "-1 2 -3 -4 -5 -1.5 -2.5 -3.5 6 7 8 -9.5 text||-1.5 -1/2.5 -1/2.5/-3 -1/2.5/-3/4.5 -1.5 -1/2.5 -1/2.5/-3 -1/2.5/-3/4.5",
                generator.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // Property names whose ordinal (UTF-16 code unit) order differs from culture-sensitive order: upper case against
    // lower case (Name/etag, Foo/foo, Mode/kind, Read/execute), camel-case prefixes (maxItems/maximum), and letters
    // that particular cultures collate differently (Turkish dotless i, Danish aa and å).
    private const string OrderingSchema =
        """
        {
          "type": "object",
          "properties": {
            "Name": { "type": "string" },
            "etag": { "type": "string" },
            "_links": { "type": "string" },
            "foo": { "type": "integer" },
            "Foo": { "type": "string" },
            "maxItems": { "type": "integer" },
            "maximum": { "type": "number", "minimum": 1.5, "multipleOf": 0.25, "exclusiveMinimum": -3 },
            "id": { "type": "string" },
            "ı": { "type": "string" },
            "aa": { "type": "string" },
            "å": { "type": "string" },
            "permissions": {
              "type": "object",
              "properties": {
                "Read": { "type": "boolean" },
                "execute": { "type": "boolean" }
              }
            },
            "variant": {
              "oneOf": [
                {
                  "type": "object",
                  "required": [ "Name", "etag" ],
                  "properties": { "Name": { "type": "string" }, "etag": { "type": "string" } }
                },
                {
                  "type": "object",
                  "properties": { "kind": { "const": "a" }, "Mode": { "const": "fast" } }
                }
              ]
            }
          }
        }
        """;

    [TestMethod]
    public async Task GenerateCode_IsCultureInvariant()
    {
        if (CultureInfo.GetCultureInfo("en-US").CompareInfo.Compare("a", "B") > 0)
        {
            Assert.Inconclusive("This process compares strings ordinally in every culture (globalization-invariant mode), so this test cannot show culture dependence.");
        }

        string directory = Directory.CreateTempSubdirectory("ordering-").FullName;
        try
        {
            string schemaPath = Path.Combine(directory, "ordering.json");
            await File.WriteAllTextAsync(schemaPath, OrderingSchema);
            IReadOnlyCollection<GeneratedCodeFile> reference = await GenerateFileUnderCulture(CultureInfo.InvariantCulture, schemaPath);
            foreach (string name in new[] { "tr-TR", "da-DK", "sv-SE", "de-DE" })
            {
                AssertSameFiles(name, reference, await GenerateFileUnderCulture(CultureInfo.GetCultureInfo(name), schemaPath));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task GenerateCode_PropertyOrder_IsOrdinal()
    {
        string directory = Directory.CreateTempSubdirectory("ordering-").FullName;
        try
        {
            string schemaPath = Path.Combine(directory, "ordering.json");
            await File.WriteAllTextAsync(schemaPath, OrderingSchema);
            IReadOnlyCollection<GeneratedCodeFile> files = await GenerateFileUnderCulture(CultureInfo.InvariantCulture, schemaPath);
            string[] lines = files.SelectMany(f => f.FileContent.Split('\n')).Select(l => l.Trim()).ToArray();

            // Positional factory parameters, flag bits, which JSON name keeps the .NET name Foo, and the type names that
            // the required-property and const-property heuristics build from property order.
            string[] report =
            [
                .. lines.Where(l => l.StartsWith("public static ParsedJsonDocument<", StringComparison.Ordinal) && l.Contains(" Create(in ", StringComparison.Ordinal)),
                .. lines.Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\w+ = 1 << \d+,$")),
                .. lines.Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^public const string (Foo|Foo1|Name|Etag) = ")),
                .. files.Select(f => f.FileName).Where(n => (n.Contains("Required", StringComparison.Ordinal) || n.Contains("With", StringComparison.Ordinal)) && !n.Contains(".Mutable.", StringComparison.Ordinal) && !n.Contains(".JsonSchema.", StringComparison.Ordinal)),
            ];

            // Ordinal order puts upper case before lower case and ASCII before other letters: Foo, Name, _links, aa, etag,
            // foo, id, maxItems, maximum, permissions, variant, å, ı. JSON Foo keeps the .NET name Foo (foo becomes Foo1),
            // Read takes bit 0, and the heuristics name RequiredNameAndEtag and WithModeFastAndKindA.
            AssertLines(
                [
                    "public static ParsedJsonDocument<Ordering> Create(in TestGenerated.JsonString.Source foo = default, in TestGenerated.JsonString.Source name = default, in TestGenerated.JsonString.Source links = default, in TestGenerated.JsonString.Source aa = default, in TestGenerated.JsonString.Source etag = default, in TestGenerated.JsonInteger.Source foo1 = default, in TestGenerated.JsonString.Source id = default, in TestGenerated.JsonInteger.Source maxItems = default, in TestGenerated.Ordering.MaximumEntity.Source maximum = default, in TestGenerated.Ordering.PermissionsEntity.Source permissions = default, in TestGenerated.Ordering.VariantEntity.Source variant = default, in TestGenerated.JsonString.Source å = default, in TestGenerated.JsonString.Source ı = default, int initialCapacity = 30)",
                    "public static ParsedJsonDocument<PermissionsEntity> Create(in TestGenerated.JsonBoolean.Source read = default, in TestGenerated.JsonBoolean.Source execute = default, int initialCapacity = 30)",
                    "public static ParsedJsonDocument<RequiredNameAndEtag> Create(in TestGenerated.JsonString.Source name, in TestGenerated.JsonString.Source etag, int initialCapacity = 30)",
                    "public static ParsedJsonDocument<WithModeFastAndKindA> Create(in TestGenerated.Ordering.VariantEntity.WithModeFastAndKindA.ModeEntity.Source mode = default, in TestGenerated.Ordering.VariantEntity.WithModeFastAndKindA.KindEntity.Source kind = default, int initialCapacity = 30)",
                    "Read = 1 << 0,",
                    "Execute = 1 << 1,",
                    "public const string Foo = \"Foo\";",
                    "public const string Name = \"Name\";",
                    "public const string Etag = \"etag\";",
                    "public const string Foo1 = \"foo\";",
                    "public const string Name = \"Name\";",
                    "public const string Etag = \"etag\";",
                    "Ordering.VariantEntity.RequiredNameAndEtag.cs",
                    "Ordering.VariantEntity.WithModeFastAndKindA.cs",
                    "Ordering.VariantEntity.WithModeFastAndKindA.ModeEntity.cs",
                    "Ordering.VariantEntity.WithModeFastAndKindA.KindEntity.cs",
                ],
                report);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task<IReadOnlyCollection<GeneratedCodeFile>> GenerateFileUnderCulture(CultureInfo culture, string schemaPath)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return await InProcessGenerationTests.GenerateInProcess(schemaPath);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private static void AssertSameFiles(string culture, IReadOnlyCollection<GeneratedCodeFile> expected, IReadOnlyCollection<GeneratedCodeFile> actual)
    {
        Dictionary<string, string> actualByName = actual.ToDictionary(f => f.FileName, f => f.FileContent, StringComparer.Ordinal);
        List<string> problems = [];
        foreach (GeneratedCodeFile file in expected)
        {
            if (!actualByName.TryGetValue(file.FileName, out string content))
            {
                problems.Add($"missing {file.FileName}");
                continue;
            }

            if (!string.Equals(content, file.FileContent, StringComparison.Ordinal))
            {
                string[] e = file.FileContent.Split('\n');
                string[] a = content.Split('\n');
                int i = 0;
                while (i < e.Length && i < a.Length && string.Equals(e[i], a[i], StringComparison.Ordinal))
                {
                    i++;
                }

                problems.Add($"{file.FileName} line {i + 1}: invariant '{(i < e.Length ? e[i].Trim() : string.Empty)}', {culture} '{(i < a.Length ? a[i].Trim() : string.Empty)}'");
            }
        }

        foreach (string name in actualByName.Keys.Except(expected.Select(f => f.FileName), StringComparer.Ordinal))
        {
            problems.Add($"extra {name}");
        }

        if (problems.Count > 0)
        {
            Assert.Fail($"Generation under {culture} differs from generation under the invariant culture in {problems.Count} files:\n{string.Join("\n", problems.Take(20))}");
        }
    }

    private static async Task<IReadOnlyCollection<GeneratedCodeFile>> GenerateUnderCulture(
        CultureInfo culture,
        string schema,
        CSharpLanguageProvider.Options options = null)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return await InProcessGenerationTests.GenerateInProcessFromContent(schema, options);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private static string[] GetSharedSimpleTypeFileNames(IReadOnlyCollection<GeneratedCodeFile> files)
    {
        return files
            .Select(f => f.FileName)
            .Where(n => n.StartsWith("Json", StringComparison.Ordinal))
            .ToArray();
    }

    private static void AssertLines(string[] expected, string[] actual)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            Assert.Fail($"Expected:\n{string.Join("\n", expected)}\nActual:\n{string.Join("\n", actual)}");
        }
    }

    private static void AssertCompiles(IReadOnlyCollection<GeneratedCodeFile> files)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(GeneratedDocumentationTests.ReadCompilationDefines());

        List<SyntaxTree> trees = files
            .Select(f => CSharpSyntaxTree.ParseText(f.FileContent, parseOptions, path: f.FileName))
            .ToList();

        // Generated code is consumed from SDK projects with ImplicitUsings enabled.
        trees.Add(CSharpSyntaxTree.ParseText(
            """
            global using global::System;
            global using global::System.Collections.Generic;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;
            """,
            parseOptions,
            path: "ImplicitUsings.cs"));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "DeterministicGenerationCheck",
            trees,
            GeneratedDocumentationTests.BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(true));

        // CS8795: [GeneratedRegex] partial methods are implemented by the regex source generator in a consuming
        // project's build, which this compilation does not run.
        string[] errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != "CS8795")
            .Select(d => d.ToString())
            .ToArray();

        if (errors.Length > 0)
        {
            Assert.Fail($"The generated code has {errors.Length} compilation errors:\n{string.Join("\n", errors.Take(25))}");
        }
    }
}