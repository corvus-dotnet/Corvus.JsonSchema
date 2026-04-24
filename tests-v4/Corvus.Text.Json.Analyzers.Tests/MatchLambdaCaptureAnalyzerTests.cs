// <copyright file="MatchLambdaCaptureAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Analyzers.MatchLambdaCaptureAnalyzer,
    Corvus.Text.Json.Analyzers.MatchLambdaCaptureCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Analyzers.MatchLambdaCaptureAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ003: Match lambda should be static.
/// </summary>
public class MatchLambdaCaptureAnalyzerTests
{
    private const string Stubs = @"
using System;

namespace TestLib
{
    public delegate TOut Matcher<TMatch, TOut>(in TMatch match);
    public delegate TOut Matcher<TMatch, TIn, TOut>(in TMatch match, in TIn context);

    public struct JsonString { public override string ToString() => """"; }
    public struct JsonNumber
    {
        public override string ToString() => """";
        public static bool operator >(JsonNumber a, int b) => false;
        public static bool operator <(JsonNumber a, int b) => false;
    }

    public struct JsonElement
    {
        public TOut Match<TOut>(
            Matcher<JsonString, TOut> match0,
            Matcher<JsonNumber, TOut> match1,
            Matcher<JsonElement, TOut> defaultMatch) => default;

        public TOut Match<TIn, TOut>(
            in TIn context,
            Matcher<JsonString, TIn, TOut> match0,
            Matcher<JsonNumber, TIn, TOut> match1,
            Matcher<JsonElement, TIn, TOut> defaultMatch) => default;
    }
}
";

    [Fact]
    public async Task NonStaticNonCapturingLambdas_FiresCTJ003()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                {|#0:(in TestLib.JsonString s) => s.ToString()|},
                {|#1:(in TestLib.JsonNumber n) => n.ToString()|},
                {|#2:(in TestLib.JsonElement e) => ""other""|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("consider adding the 'static' modifier"),
            Verify.Diagnostic().WithLocation(1).WithArguments("consider adding the 'static' modifier"),
            Verify.Diagnostic().WithLocation(2).WithArguments("consider adding the 'static' modifier"));
    }

    [Fact]
    public async Task AlreadyStaticLambdas_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                static (in TestLib.JsonString s) => s.ToString(),
                static (in TestLib.JsonNumber n) => n.ToString(),
                static (in TestLib.JsonElement e) => ""other"");
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CapturingLambda_FiresCTJ003WithCaptureMessage()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            int threshold = 10;
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                {|#0:(in TestLib.JsonString s) => s.ToString()|},
                {|#1:(in TestLib.JsonNumber n) => n > threshold ? ""high"" : ""low""|},
                {|#2:(in TestLib.JsonElement e) => ""other""|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("consider adding the 'static' modifier"),
            Verify.Diagnostic().WithLocation(1).WithArguments("consider using Match<TContext, TResult> to avoid closure allocation"),
            Verify.Diagnostic().WithLocation(2).WithArguments("consider adding the 'static' modifier"));
    }

    [Fact]
    public async Task BlockBodyCapturingLambda_FiresCTJ003WithCaptureMessage()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            int threshold = 10;
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                {|#0:(in TestLib.JsonString s) => { return s.ToString(); }|},
                {|#1:(in TestLib.JsonNumber n) => { return n > threshold ? ""high"" : ""low""; }|},
                {|#2:(in TestLib.JsonElement e) => { return ""other""; }|});
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(
            testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("consider adding the 'static' modifier"),
            Verify.Diagnostic().WithLocation(1).WithArguments("consider using Match<TContext, TResult> to avoid closure allocation"),
            Verify.Diagnostic().WithLocation(2).WithArguments("consider adding the 'static' modifier"));
    }

    [Fact]
    public async Task CodeFix_AddsStaticToNonCapturingLambdas()
    {
        var test = new CodeFixTest
        {
            TestCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                {|#0:(in TestLib.JsonString s) => s.ToString()|},
                {|#1:(in TestLib.JsonNumber n) => n.ToString()|},
                {|#2:(in TestLib.JsonElement e) => ""other""|});
        }
    }
}",
            FixedCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            var element = new TestLib.JsonElement();
            return element.Match<string>(
                static (in TestLib.JsonString s) => s.ToString(),
                static (in TestLib.JsonNumber n) => n.ToString(),
                static (in TestLib.JsonElement e) => ""other"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("consider adding the 'static' modifier"));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(1).WithArguments("consider adding the 'static' modifier"));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(2).WithArguments("consider adding the 'static' modifier"));
        await test.RunAsync();
    }

    [Fact]
    public async Task ContextOverload_NoDiagnostic()
    {
        // Using Match<TIn, TOut> with context — no diagnostic since it already has context.
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        string M()
        {
            int threshold = 10;
            var element = new TestLib.JsonElement();
            return element.Match<int, string>(
                in threshold,
                static (in TestLib.JsonString s, in int _) => s.ToString(),
                static (in TestLib.JsonNumber n, in int ctx) => n > ctx ? ""high"" : ""low"",
                static (in TestLib.JsonElement e, in int _) => ""other"");
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NonMatchMethod_NoDiagnostic()
    {
        // A non-Match method with lambda args should not trigger.
        const string testCode = @"
using System;

namespace TestApp
{
    class Test
    {
        void M()
        {
            Func<int, string> fn = (x) => x.ToString();
            var result = fn(42);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}