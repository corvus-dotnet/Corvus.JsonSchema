// <copyright file="CoreTypeAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.CoreTypeAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.CoreTypeCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using VerifyCVJ007 = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.CoreTypeAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ007 (JsonAny → JsonElement) and CVJ009 (typed core types).
/// </summary>
public class CoreTypeAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
    public struct JsonAny : IJsonValue { }
    public struct JsonObject : IJsonValue { }
    public struct JsonString : IJsonValue { }
}
";

    [Fact]
    public async Task JsonAny_TriggersCVJ007_AndCodeFixReplacesWithJsonElement()
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
            Corvus.Json.{|#0:JsonAny|} value = default;
        }
    }
}",
            FixedCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonElement value = default;
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            VerifyCVJ007.Diagnostic("CVJ007")
                .WithLocation(0)
                .WithArguments("JsonAny"));

        await test.RunAsync();
    }

    [Fact]
    public async Task JsonObject_TriggersCVJ009()
    {
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.{|#0:JsonObject|} value = default;
        }
    }
}";

        await VerifyCVJ007.VerifyAnalyzerAsync(
            testCode,
            VerifyCVJ007.Diagnostic("CVJ009")
                .WithLocation(0)
                .WithArguments("JsonObject"));
    }

    [Fact]
    public async Task NonCorvusJsonAny_NoDiagnostic()
    {
        const string testCode = @"
namespace OtherLib
{
    struct JsonAny { }
}

namespace TestApp
{
    class Test
    {
        void M()
        {
            OtherLib.JsonAny value = default;
        }
    }
}";

        await VerifyCVJ007.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task JsonAnyInsideUsingDirective_NoDiagnostic()
    {
        // CVJ001 handles using directives, not CVJ007
        const string testCode = @"
using Corvus.Json;

namespace Corvus.Json
{
    public interface IJsonValue { }
    public struct JsonAny : IJsonValue { }
}

namespace TestApp
{
    class Test
    {
        void M() { }
    }
}";

        await VerifyCVJ007.VerifyAnalyzerAsync(testCode);
    }
}