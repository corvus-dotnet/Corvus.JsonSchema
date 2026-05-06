// <copyright file="CreateFactoryAnalyzerTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.CreateAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.CreateFactoryCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.CreateAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ013/CVJ014/CVJ015: Create, FromItems, FromValues migration.
/// </summary>
public class CreateFactoryAnalyzerTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct JsonArray : IJsonValue
    {
        public static JsonArray Create(params JsonAny[] items) => default;
        public static JsonArray FromItems(params JsonAny[] items) => default;
        public static JsonArray FromValues(int[] values) => default;
        public int GetPropertyCount() => 0;
        public void WriteTo(object writer) { }
    }

    public struct JsonAny : IJsonValue { }
    public struct JsonNumber : IJsonValue { }
    public struct JsonString : IJsonValue { }

    public struct Person : IJsonValue
    {
        public static Person Create(string name, int age) => default;
        public JsonArray Tags => default;
        public void SetTags(JsonArray value) { }
    }
}

public struct JsonWorkspace : System.IDisposable
{
    public static JsonWorkspace Create() => default;
    public void Dispose() { }
}
";

    // ── Top-level (Builder) tests ────────────────────────────────────
    [Fact]
    public async Task Create_TopLevel_RewritesToCreateBuilder()
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
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:Create|}(
                default(Corvus.Json.JsonAny),
                default(Corvus.Json.JsonAny));
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
            using var workspace = JsonWorkspace.Create();
            var arr = Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny), default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_TopLevel_ReusesExistingWorkspace()
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
            using var ws = JsonWorkspace.Create();
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:Create|}(
                default(Corvus.Json.JsonAny));
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
            using var ws = JsonWorkspace.Create();
            var arr = Corvus.Json.JsonArray.CreateBuilder(ws, default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_UsedAsInstance_RewritesToCreateBuilder()
    {
        // Variable has member access → it's a builder, not a source.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:Create|}(
                default(Corvus.Json.JsonAny));
            arr.WriteTo(null);
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
            using var workspace = JsonWorkspace.Create();
            var arr = Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny));
            arr.RootElement.WriteTo(null);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    // ── Source (Build) tests ─────────────────────────────────────────
    [Fact]
    public async Task Create_DirectArgument_RewritesToBuild()
    {
        // Create() used directly as an argument → Source → Build().
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.Person person = default;
            person.SetTags(Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny)));
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
            Corvus.Json.Person person = default;
            person.SetTags(Corvus.Json.JsonArray.Build(default(Corvus.Json.JsonAny)));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_AssignedThenPassedAsArgument_RewritesToBuild()
    {
        // Variable assigned from Create(), then used only as argument → Source → Build().
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.Person person = default;
            Corvus.Json.JsonArray tags = Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
            person.SetTags(tags);
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
            Corvus.Json.Person person = default;
            Corvus.Json.JsonArray.Source tags = Corvus.Json.JsonArray.Build(default(Corvus.Json.JsonAny));
            person.SetTags(tags);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    // ── FromItems (CVJ014) tests ─────────────────────────────────────
    [Fact]
    public async Task FromItems_TopLevel_RewritesToCreateBuilderWithBuild()
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
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:FromItems|}(
                default(Corvus.Json.JsonAny),
                default(Corvus.Json.JsonAny));
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
            using var workspace = JsonWorkspace.Create();
            var arr = Corvus.Json.JsonArray.CreateBuilder(workspace, Build(
                default(Corvus.Json.JsonAny),
                default(Corvus.Json.JsonAny)));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ014").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task FromItems_DirectArgument_RewritesToBuild()
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
            Corvus.Json.Person person = default;
            person.SetTags(Corvus.Json.JsonArray.{|#0:FromItems|}(default(Corvus.Json.JsonAny)));
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
            Corvus.Json.Person person = default;
            person.SetTags(Corvus.Json.JsonArray.Build(default(Corvus.Json.JsonAny)));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ014").WithLocation(0));
        await test.RunAsync();
    }

    // ── FromValues (CVJ015) tests ────────────────────────────────────
    [Fact]
    public async Task FromValues_TopLevel_RewritesToCreateBuilder()
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
            var values = new int[] { 1, 2, 3 };
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:FromValues|}(values);
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
            var values = new int[] { 1, 2, 3 };
            using var workspace = JsonWorkspace.Create();
            var arr = Corvus.Json.JsonArray.CreateBuilder(workspace, values);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ015").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task FromValues_ReusesExistingWorkspace()
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
            using var myWorkspace = JsonWorkspace.Create();
            var values = new int[] { 1, 2, 3 };
            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:FromValues|}(values);
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
            using var myWorkspace = JsonWorkspace.Create();
            var values = new int[] { 1, 2, 3 };
            var arr = Corvus.Json.JsonArray.CreateBuilder(myWorkspace, values);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ015").WithLocation(0));
        await test.RunAsync();
    }

    // ── Edge case tests ─────────────────────────────────────────────
    [Fact]
    public async Task Create_VarType_BuildPath_DoesNotAppendSource()
    {
        // var should NOT be rewritten to var.Source
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.Person person = default;
            var tags = Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
            person.SetTags(tags);
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
            Corvus.Json.Person person = default;
            var tags = Corvus.Json.JsonArray.Build(default(Corvus.Json.JsonAny));
            person.SetTags(tags);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_WorkspaceOutOfScope_InsertsNew()
    {
        // Workspace declared in a different block should NOT be reused.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            {
                using var ws = JsonWorkspace.Create();
            }

            Corvus.Json.JsonArray arr = Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
            arr.WriteTo(null);
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
            {
                using var ws = JsonWorkspace.Create();
            }

            using var workspace = JsonWorkspace.Create();

            var arr = Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny));
            arr.RootElement.WriteTo(null);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_ExpressionBodied_DiagnosticOnly()
    {
        // Expression-bodied member — no BlockSyntax to insert workspace into.
        // The diagnostic fires but the code fix cannot apply (no statement
        // context). We verify only the diagnostic, not a code fix.
        const string testCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        Corvus.Json.JsonArray M() => Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
    }
}";

        await Verify.VerifyAnalyzerAsync(testCode,
            Verify.Diagnostic("CVJ013").WithLocation(0));
    }

    [Fact]
    public async Task Create_FieldAssignment_TopLevel()
    {
        // Assigned to a field — no local declaration, not inside argument.
        // Should be top-level CreateBuilder.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        Corvus.Json.JsonArray _arr;

        void M()
        {
            _arr = Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
        }
    }
}",
            FixedCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        Corvus.Json.JsonArray _arr;

        void M()
        {
            using var workspace = JsonWorkspace.Create();
            _arr = Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_StandaloneStatement_TopLevel()
    {
        // Create() as standalone expression statement — no assignment at all.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny));
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
            using var workspace = JsonWorkspace.Create();
            Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }

    [Fact]
    public async Task Create_InTernary_TopLevel()
    {
        // Create() in ternary — not inside an argument list,
        // so should be top-level path.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            bool flag = true;
            Corvus.Json.JsonArray arr = flag
                ? Corvus.Json.JsonArray.{|#0:Create|}(default(Corvus.Json.JsonAny))
                : default;
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
            bool flag = true;
            using var workspace = JsonWorkspace.Create();
            var arr = flag
                ? Corvus.Json.JsonArray.CreateBuilder(workspace, default(Corvus.Json.JsonAny)) : default;
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(Verify.Diagnostic("CVJ013").WithLocation(0));
        await test.RunAsync();
    }
}