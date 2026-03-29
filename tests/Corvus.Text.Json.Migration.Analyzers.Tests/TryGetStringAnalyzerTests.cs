// <copyright file="TryGetStringAnalyzerTests.cs" company="Endjin Limited">
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

using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Corvus.Text.Json.Migration.Analyzers.MiscPatternAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.TryGetStringCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.MiscPatternAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ018: TryGetString to TryGetValue migration.
/// </summary>
public class TryGetStringAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct JsonString : IJsonValue
    {
        public bool TryGetString(out string value) { value = """"; return true; }
    }
}
";

    [Fact]
    public async Task TryGetString_TriggersCVJ018_AndCodeFixRenames()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var str = new Corvus.Json.JsonString();
            str.{|#0:TryGetString|}(out string value);
        }
    }
}",
            FixedCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var str = new Corvus.Json.JsonString();
            str.TryGetValue(out string value);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic("CVJ018").WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task TryGetString_OnNonJsonValue_NoDiagnostic()
    {
        const string testCode = @"
namespace TestApp
{
    class MyClass
    {
        public bool TryGetString(out string value) { value = """"; return true; }
    }

    class Test
    {
        void M()
        {
            var x = new MyClass();
            x.TryGetString(out string value);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}