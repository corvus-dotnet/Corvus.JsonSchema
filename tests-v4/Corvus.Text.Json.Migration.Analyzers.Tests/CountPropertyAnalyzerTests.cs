// <copyright file="CountPropertyAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.CountPropertyAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.CountPropertyCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.CountPropertyAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ005: Count property to GetPropertyCount() migration.
/// </summary>
public class CountPropertyAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct JsonObject : IJsonValue
    {
        public int Count => 3;
    }
}
";

    [Fact]
    public async Task CountPropertyAccess_TriggersCVJ005_AndCodeFixReplaces()
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
            var obj = new Corvus.Json.JsonObject();
            var count = obj.{|#0:Count|};
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
            var obj = new Corvus.Json.JsonObject();
            var count = obj.GetPropertyCount();
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic().WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task CountMethodCall_NoDiagnostic()
    {
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var list = new System.Collections.Generic.List<int>();
            var count = list.Count;
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CountInExpression_TriggersCVJ005()
    {
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var obj = new Corvus.Json.JsonObject();
            bool hasItems = obj.{|#0:Count|} > 0;
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0));
    }
}