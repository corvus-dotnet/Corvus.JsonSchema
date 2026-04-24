// <copyright file="UnnecessaryConversionAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.UnnecessaryConversionAnalyzer,
    Corvus.Text.Json.Analyzers.UnnecessaryConversionCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Analyzers.UnnecessaryConversionAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ002: Unnecessary conversion to .NET type.
/// </summary>
public class UnnecessaryConversionAnalyzerTests
{
    // Stubs: Source struct with implicit conversions, and types that use it.
    private const string Stubs = @"
using System;

namespace TestLib
{
    public readonly struct Source
    {
        private readonly object _value;

        public static implicit operator Source(JsonElement e) => default;
        public static implicit operator Source(string s) => default;
        public static implicit operator Source(bool b) => default;
        public static implicit operator Source(int i) => default;
        public static implicit operator Source(double d) => default;
    }

    public struct JsonElement
    {
        public string GetString() => """";

        public static explicit operator string(JsonElement e) => """";
        public static explicit operator int(JsonElement e) => 0;
        public static explicit operator bool(JsonElement e) => false;
        public static explicit operator double(JsonElement e) => 0.0;

        public struct Mutable
        {
            public void SetProperty(string name, Source value) { }
            public void AddItem(Source value) { }
        }
    }
}
";

    [Fact]
    public async Task CastToString_WhenSourceAcceptsOriginal_FiresCTJ002()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty(""name"", {|#0:(string)element|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("string"));
    }

    [Fact]
    public async Task CastToInt_WhenSourceAcceptsOriginal_FiresCTJ002()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty(""age"", {|#0:(int)element|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("int"));
    }

    [Fact]
    public async Task CastToString_InAddItem_FiresCTJ002()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.AddItem({|#0:(string)element|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("string"));
    }

    [Fact]
    public async Task CodeFix_RemovesCast()
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
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty(""name"", {|#0:(string)element|});
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
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty(""name"", element);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("string"));
        await test.RunAsync();
    }

    [Fact]
    public async Task NoCast_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var element = new TestLib.JsonElement();
            var mutable = new TestLib.JsonElement.Mutable();
            mutable.SetProperty(""name"", element);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CastToSameAsParameter_NoDiagnostic()
    {
        // When the cast target type IS the parameter type, it's not unnecessary.
        const string testCode = @"
namespace TestApp
{
    class MyClass
    {
        public void DoSomething(string value) { }
    }

    class Test
    {
        void M()
        {
            object obj = ""hello"";
            var c = new MyClass();
            c.DoSomething((string)obj);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CastToTypeWithNoImplicitConversion_NoDiagnostic()
    {
        // Source has no implicit conversion from Widget, so the cast to string IS necessary
        // (it's the only way to get to a Source-compatible type).
        const string testCode = Stubs + @"
namespace TestApp
{
    class Widget
    {
        public static explicit operator string(Widget w) => """";
    }

    class Processor
    {
        public void Process(TestLib.Source value) { }
    }

    class Test
    {
        void M()
        {
            var w = new Widget();
            var p = new Processor();
            p.Process((string)w);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}