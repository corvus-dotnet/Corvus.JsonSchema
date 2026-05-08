// <copyright file="AsGenericAnalyzerTests.cs" company="Endjin Limited">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

using CodeFixTest = Corvus.Text.Json.Migration.Analyzers.Tests.CodeFixTestBase<
    Corvus.Text.Json.Migration.Analyzers.AsGenericAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.AsGenericCodeFix>;
using Verify = Corvus.Text.Json.Migration.Analyzers.Tests.AnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.AsGenericAnalyzer>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ004: As&lt;T&gt;() migration to T.From(value).
/// </summary>
[TestClass]
public class AsGenericAnalyzerTests
{
    private const string V4InterfaceStubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }
}
";

    [TestMethod]
    public async Task AsGenericCall_TriggersCVJ004_AndCodeFixTransformsToFrom()
    {
        var test = new CodeFixTest
        {
            TestCode = V4InterfaceStubs + @"
namespace TestApp
{
    class SomeType
    {
        public static SomeType From(JsonValue value) => new();
    }

    class JsonValue : Corvus.Json.IJsonValue
    {
        public T As<T>() => default;
    }

    class Test
    {
        void M()
        {
            var value = new JsonValue();
            var result = {|#0:value.As<SomeType>()|};
        }
    }
}",
            FixedCode = V4InterfaceStubs + @"
namespace TestApp
{
    class SomeType
    {
        public static SomeType From(JsonValue value) => new();
    }

    class JsonValue : Corvus.Json.IJsonValue
    {
        public T As<T>() => default;
    }

    class Test
    {
        void M()
        {
            var value = new JsonValue();
            var result = SomeType.From(value);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("SomeType"));

        await test.RunAsync();
    }

    [TestMethod]
    public async Task RegularGenericMethodCall_NoDiagnostic()
    {
        const string testCode = @"
namespace TestApp
{
    class MyService
    {
        public T Get<T>() => default;
    }

    class Test
    {
        void M()
        {
            var svc = new MyService();
            var result = svc.Get<string>();
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [TestMethod]
    public async Task AsGenericCall_OnNonJsonValueType_NoDiagnostic()
    {
        const string testCode = V4InterfaceStubs + @"
namespace TestApp
{
    class PlainValue
    {
        public T As<T>() => default;
    }

    class Test
    {
        void M()
        {
            var value = new PlainValue();
            var result = value.As<string>();
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }
}