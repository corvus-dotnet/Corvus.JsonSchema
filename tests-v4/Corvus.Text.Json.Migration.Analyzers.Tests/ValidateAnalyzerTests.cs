// <copyright file="ValidateAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.ValidateAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.ValidateCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.ValidateAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ003: IsValid/Validate migration to EvaluateSchema.
/// </summary>
public class ValidateAnalyzerTests
{
    private const string V4InterfaceStubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
}
";

    [Fact]
    public async Task IsValidCall_TriggersCVJ003_AndCodeFixReplacesWithEvaluateSchema()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public bool IsValid() => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = {|#0:x.IsValid()|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public bool IsValid() => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.EvaluateSchema();
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Replace '.IsValid()' with '.EvaluateSchema()'"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ValidateCall_TriggersCVJ003()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class ValidationContext
    {
        public static ValidationContext ValidContext => new();
    }

    class MyJsonType : Corvus.Json.IJsonValue
    {
        public void Validate(ValidationContext ctx) { }
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            {|#0:x.Validate(ValidationContext.ValidContext)|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("Replace '.Validate(ValidationContext.ValidContext)' with '.EvaluateSchema()'");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NoIsValidCall_NoDiagnostic()
    {
        const string testCode = """

            namespace TestApp
            {
                class MyJsonType
                {
                    public string GetValue() => "hello";
                }

                class Test
                {
                    void M()
                    {
                        var x = new MyJsonType();
                        var result = x.GetValue();
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IsValidCall_OnNonJsonValueType_NoDiagnostic()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyPlainType
    {
        public bool IsValid() => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyPlainType();
            var result = x.IsValid();
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ValidateCall_OnNonJsonValueType_NoDiagnostic()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class ValidationContext
    {
        public static ValidationContext ValidContext => new();
    }

    class MyPlainType
    {
        public void Validate(ValidationContext ctx) { }
    }

    class Test
    {
        void M()
        {
            var x = new MyPlainType();
            x.Validate(ValidationContext.ValidContext);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IsValidCall_WithExplicitType_ReplacesTypeWithBool()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class ValidationContext
    {
        public bool IsValid => true;
    }

    class MyJsonType : Corvus.Json.IJsonValue
    {
        public ValidationContext IsValid() => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            ValidationContext result = {|#0:x.IsValid()|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class ValidationContext
    {
        public bool IsValid => true;
    }

    class MyJsonType : Corvus.Json.IJsonValue
    {
        public ValidationContext IsValid() => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            bool result = x.EvaluateSchema();
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Replace '.IsValid()' with '.EvaluateSchema()'"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ValidateCall_WithExplicitType_AndCollector_ReplacesTypeWithBool()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    enum ValidationLevel { Flag, Basic, Detailed, Verbose }

    class ValidationContext
    {
        public static ValidationContext ValidContext => new();
    }

    class MyJsonType : Corvus.Json.IJsonValue
    {
        public ValidationContext Validate(ValidationContext ctx, ValidationLevel level) => ctx;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            ValidationContext result = {|#0:x.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed)|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    enum ValidationLevel { Flag, Basic, Detailed, Verbose }

    class ValidationContext
    {
        public static ValidationContext ValidContext => new();
    }

    class MyJsonType : Corvus.Json.IJsonValue
    {
        public ValidationContext Validate(ValidationContext ctx, ValidationLevel level) => ctx;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
            bool result = x.EvaluateSchema(collector);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Replace '.Validate(...)' with 'using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed); value.EvaluateSchema(collector);'"));

        await test.RunAsync();
    }

    [Fact]
    public async Task IsValidCall_WithVar_LeavesVarAlone()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public bool IsValid() => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = {|#0:x.IsValid()|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public bool IsValid() => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.EvaluateSchema();
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("Replace '.IsValid()' with '.EvaluateSchema()'"));

        await test.RunAsync();
    }
}