// <copyright file="ParsedValueAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.ParsedValueAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.ParsedValueCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.ParsedValueAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ002: ParsedValue to ParsedJsonDocument migration.
/// </summary>
public class ParsedValueAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
    public struct JsonAny : IJsonValue { }

    public struct ParsedValue<T> where T : struct, IJsonValue
    {
        public T Instance => default;
        public static ParsedValue<T> Parse(string json) => default;
    }
}
";

    // After the code fix runs, the stub's ParsedValue<T> return type is also renamed.
    private const string FixedV4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
    public struct JsonAny : IJsonValue { }

    public struct ParsedValue<T> where T : struct, IJsonValue
    {
        public T Instance => default;
        public static ParsedJsonDocument<T> Parse(string json) => default;
    }
}
";

    [Fact]
    public async Task ParsedValueGenericName_TriggersCVJ002_AndCodeFixReplaces()
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
            var parsed = Corvus.Json.{|#0:ParsedValue<Corvus.Json.JsonAny>|}.Parse(""{ }"");
        }
    }
}",
            FixedCode = FixedV4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var parsed = Corvus.Json.ParsedJsonDocument<Corvus.Json.JsonAny>.Parse(""{ }"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
            NumberOfFixAllIterations = 1,
        };

        // Stub fires CVJ002 on its own ParsedValue<T> return type at line 10
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithSpan(10, 23, 10, 37));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task InstanceProperty_TriggersCVJ002()
    {
        const string testCode = $@"{V4Stubs}
namespace TestApp
{{
    class Test
    {{
        void M()
        {{
            var parsed = Corvus.Json.{{|#1:ParsedValue<Corvus.Json.JsonAny>|}}.Parse(""{{ }}"");
            var value = parsed.{{|#0:Instance|}};
        }}
    }}
}}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithSpan(10, 23, 10, 37),
            Verify.Diagnostic().WithLocation(1),
            Verify.Diagnostic().WithLocation(0));
    }

    [Fact]
    public async Task NonParsedValueType_NoDiagnosticOnInstance()
    {
        const string testCode = @"
namespace TestApp
{
    class MyClass
    {
        public int Instance => 42;
    }

    class Test
    {
        void M()
        {
            var x = new MyClass();
            var val = x.Instance;
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UsingDeclarationWithParsedValue_TriggersCVJ002()
    {
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var parsed = Corvus.Json.{|#0:ParsedValue<Corvus.Json.JsonAny>|}.Parse(""{ }"");
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithSpan(10, 23, 10, 37),
            Verify.Diagnostic().WithLocation(0));
    }
}