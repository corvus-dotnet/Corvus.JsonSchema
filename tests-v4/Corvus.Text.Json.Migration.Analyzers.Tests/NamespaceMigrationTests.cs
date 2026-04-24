// <copyright file="NamespaceMigrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using AnalyzerTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
    Corvus.Text.Json.Migration.Analyzers.NamespaceMigrationAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Corvus.Text.Json.Migration.Analyzers.NamespaceMigrationAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.NamespaceMigrationCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.NamespaceMigrationAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ001: namespace migration from Corvus.Json to Corvus.Text.Json.
/// </summary>
public class NamespaceMigrationTests
{
    [Fact]
    public async Task UsingCorvusJson_TriggersCVJ001_AndCodeFixReplacesWithCorvusTextJson()
    {
        var test = new CodeFixTest
        {
            TestCode = @"{|#0:using Corvus.Json;|}
class C { }",
            FixedCode = @"using Corvus.Text.Json;
class C { }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Corvus.Json"));

        await test.RunAsync();
    }

    [Fact]
    public async Task UsingCorvusJsonInternal_TriggersCVJ001_AndCodeFixReplacesWithCorvusTextJsonInternal()
    {
        var test = new CodeFixTest
        {
            TestCode = @"{|#0:using Corvus.Json.Internal;|}
class C { }",
            FixedCode = @"using Corvus.Text.Json.Internal;
class C { }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Corvus.Json.Internal"));

        await test.RunAsync();
    }

    [Fact]
    public async Task UsingSystemTextJson_NoDiagnostic()
    {
        const string testCode = @"using System.Text.Json;
class C { }";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UsingCorvusTextJson_NoDiagnostic()
    {
        var test = new AnalyzerTest
        {
            TestCode = @"using Corvus.Text.Json;
class C { }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        await test.RunAsync();
    }

    [Fact]
    public async Task UsingCorvusJsonCodeGeneration_NoDiagnostic()
    {
        var test = new AnalyzerTest
        {
            TestCode = @"using Corvus.Json.CodeGeneration;
class C { }",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        await test.RunAsync();
    }
}