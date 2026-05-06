// <copyright file="FromJsonAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.FromJsonAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.FromJsonCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.FromJsonAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ006: FromJson() migration to From().
/// </summary>
public class FromJsonAnalyzerTests
{
    private const string V4InterfaceStubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
}
";

    [Fact]
    public async Task FromJsonCall_TriggersCVJ006_AndCodeFixRenamesToFrom()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class JsonElement
    {
    }

    class MyType : Corvus.Json.IJsonValue
    {
        public static MyType FromJson(JsonElement element) => new();
    }

    class Test
    {
        void M()
        {
            var element = new JsonElement();
            var result = {|#0:MyType.FromJson(element)|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class JsonElement
    {
    }

    class MyType : Corvus.Json.IJsonValue
    {
        public static MyType FromJson(JsonElement element) => new();
    }

    class Test
    {
        void M()
        {
            var element = new JsonElement();
            var result = MyType.From(element);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task InstanceFromJsonCall_TriggersCVJ006_AndCodeFixRenamesToFrom()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class JsonElement
    {
    }

    class MyType : Corvus.Json.IJsonValue
    {
        public MyType FromJson(JsonElement element) => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyType();
            var element = new JsonElement();
            var result = {|#0:x.FromJson(element)|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class JsonElement
    {
    }

    class MyType : Corvus.Json.IJsonValue
    {
        public MyType FromJson(JsonElement element) => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyType();
            var element = new JsonElement();
            var result = x.From(element);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task RegularMethodCall_NoDiagnostic()
    {
        const string testCode = """

            namespace TestApp
            {
                class MyType
                {
                    public static MyType From(string s) => new();
                }

                class Test
                {
                    void M()
                    {
                        var result = MyType.From("hello");
                    }
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task FromJsonCall_OnNonJsonValueType_NoDiagnostic()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class JsonElement
    {
    }

    class MyPlainType
    {
        public static MyPlainType FromJson(JsonElement element) => new();
    }

    class Test
    {
        void M()
        {
            var element = new JsonElement();
            var result = MyPlainType.FromJson(element);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}