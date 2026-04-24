// <copyright file="AsAccessorAnalyzerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.AsAccessorAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ010: V4 As* accessors removed in V5.
/// </summary>
public class AsAccessorAnalyzerTests
{
    private const string V4InterfaceStubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
}
";

    [Fact]
    public async Task AsString_TriggersCVJ010()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public string AsString => ""hello"";
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.{|#0:AsString|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("AsString");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AsNumber_TriggersCVJ010()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public double AsNumber => 42.0;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.{|#0:AsNumber|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("AsNumber");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AsObject_TriggersCVJ010()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public object AsObject => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.{|#0:AsObject|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("AsObject");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AsArray_TriggersCVJ010()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public object AsArray => new();
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.{|#0:AsArray|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("AsArray");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task AsBoolean_TriggersCVJ010()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyJsonType : Corvus.Json.IJsonValue
    {
        public bool AsBoolean => true;
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.{|#0:AsBoolean|};
        }
    }
}";

        DiagnosticResult expected = Verify.Diagnostic()
            .WithLocation(0)
            .WithArguments("AsBoolean");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task ToString_NoDiagnostic()
    {
        const string testCode = @"
namespace TestApp
{
    class MyJsonType
    {
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.ToString();
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RegularProperty_NoDiagnostic()
    {
        const string testCode = @"
namespace TestApp
{
    class MyJsonType
    {
        public string Name => ""test"";
    }

    class Test
    {
        void M()
        {
            var x = new MyJsonType();
            var result = x.Name;
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AsString_OnNonJsonValueType_NoDiagnostic()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class MyPlainType
    {
        public string AsString => ""hello"";
    }

    class Test
    {
        void M()
        {
            var x = new MyPlainType();
            var result = x.AsString;
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}