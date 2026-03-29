// <copyright file="PreferMemoryParseAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.PreferMemoryParseAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ010: Prefer ReadOnlyMemory/Span-based Parse overload.
/// </summary>
public class PreferMemoryParseAnalyzerTests
{
    private const string Stubs = @"
using System;
using System.Text;

namespace Corvus.Text.Json
{
    public class ParsedJsonDocument<T>
    {
        public static ParsedJsonDocument<T> Parse(string json) => new();
        public static ParsedJsonDocument<T> Parse(ReadOnlyMemory<byte> utf8Json) => new();
        public static ParsedJsonDocument<T> Parse(ReadOnlySpan<byte> utf8Json) => new();
    }

    public struct JsonElement
    {
        public static JsonElement ParseValue(string json) => default;
        public static JsonElement ParseValue(ReadOnlySpan<byte> utf8Json) => default;
        public static JsonElement ParseValue(ReadOnlySpan<char> json) => default;
    }
}
";

    // ========================
    // Pattern A: Encoding roundtrip
    // ========================
    [Fact]
    public async Task Parse_EncodingGetString_FiresCTJ010()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(byte[] data)
        {
            {|#0:Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(System.Text.Encoding.UTF8.GetString(data))|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ010", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("Pass the byte array directly using .AsMemory() instead of converting to string first"),
            },
        }.RunAsync();
    }

    // ========================
    // Pattern B: String literal
    // ========================
    [Fact]
    public async Task ParseValue_StringLiteral_FiresCTJ010()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            Corvus.Text.Json.JsonElement.ParseValue({|#0:""{}""|});
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ010", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("Use a UTF-8 string literal (\"...\"u8) or ReadOnlyMemory<byte> overload for better performance"),
            },
        }.RunAsync();
    }

    // ========================
    // Negative tests
    // ========================
    [Fact]
    public async Task Parse_WithReadOnlyMemory_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(System.ReadOnlyMemory<byte> data)
        {
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(data);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Parse_WithStringVariable_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(string json)
        {
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NonCorvusParse_StringLiteral_NoDiagnostic()
    {
        const string testCode = @"
namespace SomeOther
{
    public class Parser
    {
        public static object Parse(string json) => new();
    }
}

namespace TestApp
{
    class Test
    {
        void Method()
        {
            SomeOther.Parser.Parse(""{}"");
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ParseValue_Utf8Literal_NoDiagnostic()
    {
        // If the user already uses the u8 suffix, we don't flag it.
        // The u8 literal calls the ReadOnlySpan<byte> overload, not the string one.
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            Corvus.Text.Json.JsonElement.ParseValue(""{}""u8);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task Parse_NoBytesOverload_NoDiagnostic()
    {
        // If there's no bytes overload, don't suggest it.
        const string testCode = @"
namespace Corvus.Text.Json
{
    public class SimpleParser
    {
        public static void Parse(string json) { }
    }
}

namespace TestApp
{
    class Test
    {
        void Method()
        {
            Corvus.Text.Json.SimpleParser.Parse(""{}"");
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }
}