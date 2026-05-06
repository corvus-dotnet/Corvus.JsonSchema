// <copyright file="FunctionalArrayAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.FunctionalArrayAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.FunctionalArrayCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.FunctionalArrayAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ012: Functional array operations migration.
/// </summary>
public class FunctionalArrayAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct JsonArray : IJsonValue
    {
        public JsonArray Add(JsonAny item) => default;
        public JsonArray AddRange(JsonArray items) => default;
        public JsonArray Insert(int index, JsonAny item) => default;
        public JsonArray InsertRange(int index, JsonArray items) => default;
        public JsonArray SetItem(int index, JsonAny item) => default;
        public JsonArray RemoveAt(int index) => default;
        public JsonArray Remove(JsonAny item) => default;
        public JsonArray RemoveRange(int index, int count) => default;
        public JsonArray Replace(JsonAny oldItem, JsonAny newItem) => default;

        public struct Mutable
        {
            // V5 in-place mutation methods (void)
            public void AddItem(JsonAny item) { }
            public void AddRange(JsonArray items) { }
            public void InsertItem(int index, JsonAny item) { }
            public void InsertRange(int index, JsonArray items) { }
            public void SetItem(int index, JsonAny item) { }
            public void RemoveAt(int index) { }
            public void Remove(JsonAny item) { }
            public void RemoveRange(int index, int count) { }
            public void Replace(JsonAny oldItem, JsonAny newItem) { }
        }
    }

    public struct JsonAny : IJsonValue { }
}
";

    [Fact]
    public async Task Add_TriggersCVJ012_AndCodeFixRenames()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            arr = arr.{|#0:Add|}(item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Insert_TriggersCVJ012_AndCodeFixRenames()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            arr = arr.{|#0:Insert|}(0, item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.InsertItem(0, item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Insert", "InsertItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task SetItem_TriggersCVJ012_AndCodeFixKeepsName()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            arr = arr.{|#0:SetItem|}(0, item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.SetItem(0, item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("SetItem", "SetItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task RemoveAt_TriggersCVJ012_AndCodeFixKeepsName()
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
            Corvus.Json.JsonArray arr = default;
            arr = arr.{|#0:RemoveAt|}(0);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            arr.RemoveAt(0);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("RemoveAt", "RemoveAt"));
        await test.RunAsync();
    }

    [Fact]
    public async Task LocalDeclaration_DropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            Corvus.Json.JsonArray result = arr.{|#0:Add|}(item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task NonJsonValueType_NoDiagnostic()
    {
        const string testCode = @"
using System.Collections.Generic;

namespace TestApp
{
    class Test
    {
        void M()
        {
            var list = new List<int>();
            list.Add(42);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    // ── Edge case tests ─────────────────────────────────────────────
    [Fact]
    public async Task Add_VarType_DropsAssignment()
    {
        // var instead of explicit type
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            var result = arr.{|#0:Add|}(item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Add_AssignToDifferentVariable_DropsAssignment()
    {
        // Result assigned to a different variable than the receiver
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            Corvus.Json.JsonArray other = arr.{|#0:Add|}(item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Add_InExpressionContext_StillRenames()
    {
        // Add() used as argument — cannot drop the outer call, but
        // should still rename Add to AddItem (or at least fire diagnostic).
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void Consume(Corvus.Json.JsonArray arr) { }

        void M()
        {
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            Consume(arr.{|#0:Add|}(item));
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode,
            Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
    }

    [Fact]
    public async Task Add_InReturnStatement_PreservesReturn()
    {
        // return arr.Add(item) → arr.AddItem(item); return arr;
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        Corvus.Json.JsonArray M()
        {
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            return arr.{|#0:Add|}(item);
        }
    }
}",
            FixedCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        Corvus.Json.JsonArray M()
        {
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
            return arr;
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Add_Chained_SplitsIntoSeparateCalls()
    {
        // arr = arr.Add(a).Add(b) → arr.AddItem(a); arr.AddItem(b);
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny a = default;
            Corvus.Json.JsonAny b = default;
            arr = arr.{|#0:Add|}(a).{|#1:Add|}(b);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny a = default;
            Corvus.Json.JsonAny b = default;
            arr.AddItem(a);
            arr.AddItem(b);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
            NumberOfFixAllIterations = 1,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(1).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    [Fact]
    public async Task AlreadyMutable_NoDiagnostic()
    {
        // If the receiver is already a Mutable type (V5), no V4 diagnostic fires
        // because Mutable does not implement IJsonValue.
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.AddItem(item);
        }
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Add_ExpressionLambda_RenamesOnly()
    {
        // Expression lambda: arr => arr.Add(item)
        // The code fix should only rename, not restructure.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    delegate Corvus.Json.JsonArray Transform(Corvus.Json.JsonArray arr);

    class Test
    {
        void M()
        {
            Corvus.Json.JsonAny item = default;
            Transform t = arr => arr.{|#0:Add|}(item);
        }
    }
}",
            FixedCode = V4Stubs + @"
namespace TestApp
{
    delegate Corvus.Json.JsonArray Transform(Corvus.Json.JsonArray arr);

    class Test
    {
        void M()
        {
            Corvus.Json.JsonAny item = default;
            Transform t = arr => arr.AddItem(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Add", "AddItem"));
        await test.RunAsync();
    }

    // ── New array mutator tests ─────────────────────────────────────
    [Fact]
    public async Task AddRange_TriggersCVJ012_AndDropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonArray other = default;
            arr = arr.{|#0:AddRange|}(other);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonArray other = default;
            arr.AddRange(other);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("AddRange", "AddRange"));
        await test.RunAsync();
    }

    [Fact]
    public async Task InsertRange_TriggersCVJ012_AndDropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonArray other = default;
            arr = arr.{|#0:InsertRange|}(0, other);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonArray other = default;
            arr.InsertRange(0, other);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("InsertRange", "InsertRange"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Remove_TriggersCVJ012_AndDropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny item = default;
            arr = arr.{|#0:Remove|}(item);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny item = default;
            arr.Remove(item);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Remove", "Remove"));
        await test.RunAsync();
    }

    [Fact]
    public async Task RemoveRange_TriggersCVJ012_AndDropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            arr = arr.{|#0:RemoveRange|}(0, 3);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            arr.RemoveRange(0, 3);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("RemoveRange", "RemoveRange"));
        await test.RunAsync();
    }

    [Fact]
    public async Task Replace_TriggersCVJ012_AndDropsAssignment()
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
            Corvus.Json.JsonArray arr = default;
            Corvus.Json.JsonAny oldItem = default;
            Corvus.Json.JsonAny newItem = default;
            arr = arr.{|#0:Replace|}(oldItem, newItem);
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
            Corvus.Json.JsonArray.Mutable arr = default;
            Corvus.Json.JsonAny oldItem = default;
            Corvus.Json.JsonAny newItem = default;
            arr.Replace(oldItem, newItem);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic().WithLocation(0).WithArguments("Replace", "Replace"));
        await test.RunAsync();
    }
}