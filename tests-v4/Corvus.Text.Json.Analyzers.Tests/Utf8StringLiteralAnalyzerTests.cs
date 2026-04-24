// <copyright file="Utf8StringLiteralAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.Utf8StringLiteralAnalyzer,
    Corvus.Text.Json.Analyzers.Utf8StringLiteralCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Analyzers.Utf8StringLiteralAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ001: Prefer UTF-8 string literal.
/// </summary>
public class Utf8StringLiteralAnalyzerTests
{
    private const string Stubs = @"
using System;

namespace TestLib
{
    public struct JsonElement
    {
        // String indexer
        public JsonElement this[string propertyName] => default;

        // ReadOnlySpan<byte> indexer
        public JsonElement this[ReadOnlySpan<byte> utf8PropertyName] => default;

        // String TryGetProperty
        public bool TryGetProperty(string propertyName, out JsonElement value)
        {
            value = default;
            return false;
        }

        // ReadOnlySpan<byte> TryGetProperty
        public bool TryGetProperty(ReadOnlySpan<byte> utf8PropertyName, out JsonElement value)
        {
            value = default;
            return false;
        }

        public struct Mutable
        {
            // String SetProperty
            public void SetProperty(string propertyName, int value) { }

            // ReadOnlySpan<byte> SetProperty
            public void SetProperty(ReadOnlySpan<byte> propertyName, int value) { }

            // String RemoveProperty
            public bool RemoveProperty(string propertyName) => false;

            // ReadOnlySpan<byte> RemoveProperty
            public bool RemoveProperty(ReadOnlySpan<byte> propertyName) => false;
        }
    }
}
";

    [Fact]
    public async Task MethodWithUtf8Overload_FiresCTJ001()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            element.TryGetProperty({|#0:""name""|}, out _);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("name"));
    }

    [Fact]
    public async Task MethodWithUtf8Overload_CodeFixAppendsU8()
    {
        var test = new CodeFixTest
        {
            TestCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            element.TryGetProperty({|#0:""name""|}, out _);
        }
    }
}",
            FixedCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            element.TryGetProperty(""name""u8, out _);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("name"));
        await test.RunAsync();
    }

    [Fact]
    public async Task SetProperty_OnMutable_FiresCTJ001()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty({|#0:""age""|}, 30);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("age"));
    }

    [Fact]
    public async Task RemoveProperty_OnMutable_FiresCTJ001()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.RemoveProperty({|#0:""temp""|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("temp"));
    }

    [Fact]
    public async Task ElementAccessWithStringLiteral_FiresCTJ001()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            _ = element[{|#0:""name""|}];
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("name"));
    }

    [Fact]
    public async Task MethodWithNoUtf8Overload_NoDiagnostic()
    {
        const string testCode = @"
namespace TestApp
{
    class MyClass
    {
        public void DoSomething(string name) { }
    }

    class Test
    {
        void M()
        {
            var obj = new MyClass();
            obj.DoSomething(""hello"");
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task VariableArgument_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            string name = ""test"";
            element.TryGetProperty(name, out _);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}