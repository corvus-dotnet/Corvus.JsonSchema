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
/// Generated code must not depend on the culture of the process that generates it.
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