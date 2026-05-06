// <copyright file="MissingDisposeAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.MissingDisposeAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Corvus.Text.Json.Analyzers.MissingDisposeAnalyzer,
    Corvus.Text.Json.Analyzers.MissingDisposeCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ004/CTJ005/CTJ006: Missing dispose on Corvus.Text.Json disposable types.
/// </summary>
public class MissingDisposeAnalyzerTests
{
    private const string DisposableStubs = @"
using System;

namespace Corvus.Text.Json
{
    public class ParsedJsonDocument<T> : IDisposable
    {
        public static ParsedJsonDocument<T> Parse(string json) => new();
        public T RootElement => default!;
        public void Dispose() { }
    }

    public class JsonWorkspace : IDisposable
    {
        public static JsonWorkspace Create() => new();
        public void Dispose() { }
    }

    public class JsonDocumentBuilder<T> : IDisposable
    {
        public T RootElement => default!;
        public void Dispose() { }
    }

    public struct JsonElement
    {
        public JsonDocumentBuilder<JsonElement> BuildDocument(JsonWorkspace workspace) => new();
    }
}
";

    // ========================
    // CTJ004: ParsedJsonDocument
    // ========================
    [Fact]
    public async Task ParsedJsonDocument_WithoutUsing_FiresCTJ004()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var doc = {|#0:Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"")|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("ParsedJsonDocument"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task ParsedJsonDocument_WithUsingDeclaration_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"");
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ParsedJsonDocument_WithUsingBlock_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using (var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}""))
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ParsedJsonDocument_WithExplicitDispose_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"");
            doc.Dispose();
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task ParsedJsonDocument_ResultDiscarded_FiresCTJ004()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            {|#0:Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"")|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("ParsedJsonDocument"),
            },
        }.RunAsync();
    }

    // ========================
    // CTJ005: JsonWorkspace
    // ========================
    [Fact]
    public async Task JsonWorkspace_WithoutUsing_FiresCTJ005()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var workspace = {|#0:Corvus.Text.Json.JsonWorkspace.Create()|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ005", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("JsonWorkspace"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task JsonWorkspace_WithUsingDeclaration_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task JsonWorkspace_WithExplicitDispose_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var workspace = Corvus.Text.Json.JsonWorkspace.Create();
            workspace.Dispose();
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    // ========================
    // CTJ006: JsonDocumentBuilder
    // ========================
    [Fact]
    public async Task JsonDocumentBuilder_WithoutUsing_FiresCTJ006()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
            using var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"");
            var builder = {|#0:doc.RootElement.BuildDocument(workspace)|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ006", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("JsonDocumentBuilder"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task JsonDocumentBuilder_WithUsingDeclaration_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
            using var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"");
            using var builder = doc.RootElement.BuildDocument(workspace);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    // ========================
    // Code fix tests
    // ========================
    [Fact]
    public async Task CodeFix_AddsUsingKeyword()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var doc = {|#0:Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"")|};
        }
    }
}";

        const string fixedCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var doc = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(""{}"");
        }
    }
}";

        await new CodeFixTest
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("ParsedJsonDocument"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task CodeFix_WorkspaceAddsUsing()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var workspace = {|#0:Corvus.Text.Json.JsonWorkspace.Create()|};
        }
    }
}";

        const string fixedCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        }
    }
}";

        await new CodeFixTest
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ005", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("JsonWorkspace"),
            },
        }.RunAsync();
    }

    // ========================
    // Negative tests
    // ========================
    [Fact]
    public async Task NonCorvusDisposable_NoDiagnostic()
    {
        const string testCode = @"
using System;

namespace SomeOther
{
    public class ParsedJsonDocument<T> : IDisposable
    {
        public static ParsedJsonDocument<T> Parse(string json) => new();
        public void Dispose() { }
    }
}

namespace TestApp
{
    class Test
    {
        void Method()
        {
            var doc = SomeOther.ParsedJsonDocument<int>.Parse(""{}"");
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task UnrelatedLocalVariable_NoDiagnostic()
    {
        const string testCode = DisposableStubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var x = 42;
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }
}