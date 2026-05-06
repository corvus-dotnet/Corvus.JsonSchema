// <copyright file="JsonDocumentParseAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.JsonDocumentParseAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.JsonDocumentParseCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.JsonDocumentParseAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ008: JsonDocument.Parse migration to ParsedJsonDocument.
/// </summary>
public class JsonDocumentParseAnalyzerTests
{
    private const string StjStubs = @"
namespace System.Text.Json
{
    public sealed class JsonDocument : IDisposable
    {
        public JsonElement RootElement => default;
        public static JsonDocument Parse(string json) => new JsonDocument();
        public void Dispose() { }
    }

    public struct JsonElement { }
}
";

    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct MyType : IJsonValue
    {
        public static MyType FromJson(System.Text.Json.JsonElement element) => default;
    }
}
";

    [Fact]
    public async Task JsonDocumentParse_TriggersCVJ008()
    {
        const string testCode = StjStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = {|#0:System.Text.Json.JsonDocument.Parse(""{ }"")|};
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode, Verify.Diagnostic().WithLocation(0));
    }

    [Fact]
    public async Task SimpleReplace_CodeFix()
    {
        var test = new CodeFixTest
        {
            TestCode = StjStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = {|#0:System.Text.Json.JsonDocument.Parse(""{ }"")|};
        }
    }
}",
            FixedCode = StjStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = ParsedJsonDocument<JsonElement>.Parse(""{ }"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task ParseWithFromJson_CollapsesIntoTypedParse()
    {
        var test = new CodeFixTest
        {
            TestCode = StjStubs + V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = {|#0:System.Text.Json.JsonDocument.Parse(""{ }"")|};
            Corvus.Json.MyType element = Corvus.Json.MyType.FromJson(doc.RootElement);
        }
    }
}",
            FixedCode = StjStubs + V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = ParsedJsonDocument<Corvus.Json.MyType>.Parse(""{ }"");
            Corvus.Json.MyType element = doc.RootElement;
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task NonJsonDocumentParse_NoDiagnostic()
    {
        const string testCode = @"
namespace MyLib
{
    class JsonDocument
    {
        public static JsonDocument Parse(string json) => new JsonDocument();
    }
}

namespace TestApp
{
    class Test
    {
        void M()
        {
            var doc = MyLib.JsonDocument.Parse(""{ }"");
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    // ── Edge case tests ─────────────────────────────────────────────
    [Fact]
    public async Task ParseWithFromJson_ExplicitType_CollapsesCorrectly()
    {
        // Explicit JsonDocument type instead of var
        var test = new CodeFixTest
        {
            TestCode = StjStubs + V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using System.Text.Json.JsonDocument doc = {|#0:System.Text.Json.JsonDocument.Parse(""{ }"")|};
            Corvus.Json.MyType element = Corvus.Json.MyType.FromJson(doc.RootElement);
        }
    }
}",
            FixedCode = StjStubs + V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            using var doc = ParsedJsonDocument<Corvus.Json.MyType>.Parse(""{ }"");
            Corvus.Json.MyType element = doc.RootElement;
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task ParseWithRootElementUsedTwice_DoesNotCollapse()
    {
        // doc.RootElement used in two places — unsafe to collapse
        // Should still replace JsonDocument.Parse but NOT collapse statements.
        var test = new CodeFixTest
        {
            TestCode = $@"{StjStubs}{V4Stubs}
namespace TestApp
{{
    class Test
    {{
        void M()
        {{
            using var doc = {{|#0:System.Text.Json.JsonDocument.Parse(""{{ }}"")|}};
            Corvus.Json.MyType element = Corvus.Json.MyType.FromJson(doc.RootElement);
            var raw = doc.RootElement;
        }}
    }}
}}",
            FixedCode = $@"{StjStubs}{V4Stubs}
namespace TestApp
{{
    class Test
    {{
        void M()
        {{
            using var doc = ParsedJsonDocument<Corvus.Json.MyType>.Parse(""{{ }}"");
            Corvus.Json.MyType element = doc.RootElement;
            var raw = doc.RootElement;
        }}
    }}
}}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));
        await test.RunAsync();
    }
}