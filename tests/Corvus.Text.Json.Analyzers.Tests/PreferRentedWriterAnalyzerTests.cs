// <copyright file="PreferRentedWriterAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.PreferRentedWriterAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ009: Prefer renting Utf8JsonWriter from workspace.
/// </summary>
public class PreferRentedWriterAnalyzerTests
{
    private const string Stubs = @"
using System;
using System.IO;

namespace Corvus.Text.Json
{
    public class JsonWorkspace : IDisposable
    {
        public static JsonWorkspace Create() => new();
        public Utf8JsonWriter RentWriter(Stream stream) => new(stream);
        public void ReturnWriter(Utf8JsonWriter writer) { }
        public void Dispose() { }
    }

    public class Utf8JsonWriter
    {
        public Utf8JsonWriter(Stream stream) { }
        public void Dispose() { }
    }
}
";

    [Fact]
    public async Task NewUtf8JsonWriter_WithWorkspaceInScope_FiresCTJ009()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
            var writer = {|#0:new Corvus.Text.Json.Utf8JsonWriter(System.IO.Stream.Null)|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ009", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task NewUtf8JsonWriter_WithWorkspaceAsParameter_FiresCTJ009()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonWorkspace workspace)
        {
            var writer = {|#0:new Corvus.Text.Json.Utf8JsonWriter(System.IO.Stream.Null)|};
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ009", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task NewUtf8JsonWriter_WithoutWorkspace_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var writer = new Corvus.Text.Json.Utf8JsonWriter(System.IO.Stream.Null);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task RentWriter_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
            var writer = workspace.RentWriter(System.IO.Stream.Null);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NonCorvusUtf8JsonWriter_NoDiagnostic()
    {
        const string testCode = @"
using System;
using System.IO;

namespace SomeOther
{
    public class JsonWorkspace : IDisposable
    {
        public static JsonWorkspace Create() => new();
        public void Dispose() { }
    }

    public class Utf8JsonWriter
    {
        public Utf8JsonWriter(Stream stream) { }
    }
}

namespace TestApp
{
    class Test
    {
        void Method()
        {
            using var workspace = SomeOther.JsonWorkspace.Create();
            var writer = new SomeOther.Utf8JsonWriter(Stream.Null);
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NewUtf8JsonWriter_WorkspaceDeclaredAfter_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var writer = new Corvus.Text.Json.Utf8JsonWriter(System.IO.Stream.Null);
            using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }
}