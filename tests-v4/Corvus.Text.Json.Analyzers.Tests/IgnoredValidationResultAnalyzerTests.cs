// <copyright file="IgnoredValidationResultAnalyzerTests.cs" company="Endjin Limited">
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

using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Analyzers.IgnoredValidationResultAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ007: Ignored schema validation result.
/// </summary>
public class IgnoredValidationResultAnalyzerTests
{
    private const string Stubs = @"
using System;

namespace Corvus.Text.Json
{
    public interface IJsonSchemaResultsCollector { }

    public interface IJsonElement<T> where T : struct
    {
        bool EvaluateSchema();
        bool EvaluateSchema(IJsonSchemaResultsCollector collector);
    }

    public struct JsonElement : IJsonElement<JsonElement>
    {
        public bool EvaluateSchema() => true;
        public bool EvaluateSchema(IJsonSchemaResultsCollector collector) => true;
    }
}
";

    [Fact]
    public async Task EvaluateSchema_ResultDiscarded_FiresCTJ007()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var element = new Corvus.Text.Json.JsonElement();
            {|#0:element.EvaluateSchema()|};
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_ResultUsedInIf_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var element = new Corvus.Text.Json.JsonElement();
            if (element.EvaluateSchema())
            {
            }
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_ResultAssigned_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var element = new Corvus.Text.Json.JsonElement();
            bool isValid = element.EvaluateSchema();
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_ResultReturned_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        bool Method()
        {
            var element = new Corvus.Text.Json.JsonElement();
            return element.EvaluateSchema();
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_OnGeneratedType_FiresCTJ007()
    {
        const string stubs = @"
using System;

namespace Corvus.Text.Json
{
    public interface IJsonElement<T> where T : struct
    {
        bool EvaluateSchema();
    }
}

namespace MyApp
{
    public struct Widget : Corvus.Text.Json.IJsonElement<Widget>
    {
        public bool EvaluateSchema() => true;
    }
}
";

        const string testCode = stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var w = new MyApp.Widget();
            {|#0:w.EvaluateSchema()|};
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task NonCorvusEvaluateSchema_NoDiagnostic()
    {
        const string testCode = @"
namespace SomeOther
{
    public struct Thing
    {
        public bool EvaluateSchema() => true;
    }
}

namespace TestApp
{
    class Test
    {
        void Method()
        {
            var t = new SomeOther.Thing();
            t.EvaluateSchema();
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_ResultUsedInNegation_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method()
        {
            var element = new Corvus.Text.Json.JsonElement();
            bool isInvalid = !element.EvaluateSchema();
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task EvaluateSchema_WithCollectorResultDiscarded_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.IJsonSchemaResultsCollector collector)
        {
            var element = new Corvus.Text.Json.JsonElement();
            element.EvaluateSchema(collector);
        }
    }
}";

        await new CSharpAnalyzerTest<IgnoredValidationResultAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
        }.RunAsync();
    }
}