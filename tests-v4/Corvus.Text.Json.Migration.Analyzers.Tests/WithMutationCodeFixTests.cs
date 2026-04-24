// <copyright file="WithMutationCodeFixTests.cs" company="Endjin Limited">
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
    Corvus.Text.Json.Migration.Analyzers.WithMutationAnalyzer,
    Corvus.Text.Json.Migration.Analyzers.WithMutationCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Corvus.Text.Json.Migration.Analyzers.WithMutationAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Tests for CVJ011 code fix: With*() to Set*() with unchaining and nested collapse.
/// </summary>
public class WithMutationCodeFixTests
{
    private const string V4Stubs = @"
namespace Corvus.Json
{
    public interface IJsonValue { }

    public struct JsonString : IJsonValue
    {
        private readonly string _value;
        public JsonString(string v) { _value = v; }
        public static explicit operator JsonString(string v) => new JsonString(v);
    }

    public struct JsonAny : IJsonValue { }
}
";

    private const string PersonStubs = @"
namespace TestApp
{
    struct Address : Corvus.Json.IJsonValue
    {
        public Address WithCity(string city) => this;
        public Address WithStreet(string street) => this;
        public Address SetProperty(string name, Corvus.Json.JsonAny value) => this;
        public Address RemoveProperty(string name) => this;

        public struct Mutable
        {
            public void SetCity(string city) { }
            public void SetStreet(string street) { }
            public void SetProperty(string name, Corvus.Json.JsonAny value) { }
            public void RemoveProperty(string name) { }
        }
    }

    struct TagsArray : Corvus.Json.IJsonValue
    {
        public TagsArray Add(Corvus.Json.JsonString value) => this;

        public struct Mutable
        {
            public void AddItem(Corvus.Json.JsonString value) { }
        }
    }

    struct Person : Corvus.Json.IJsonValue
    {
        public Address AddressValue => default;
        public TagsArray Tags => default;
        public Person WithName(string name) => this;
        public Person WithAge(int age) => this;
        public Person WithAddressValue(Address addr) => this;
        public Person WithTags(TagsArray tags) => this;
        public Person SetProperty(string name, Corvus.Json.JsonAny value) => this;
        public Person RemoveProperty(string name) => this;

        public struct Mutable
        {
            public Address.Mutable AddressValue => default;
            public void SetName(string name) { }
            public void SetAge(int age) { }
            public void SetAddressValue(Address addr) { }
            public void SetTags(TagsArray tags) { }
            public void SetProperty(string name, Corvus.Json.JsonAny value) { }
            public void RemoveProperty(string name) { }
        }
    }
}
";

    [Fact]
    public async Task SimpleWith_RenamesAndDropsAssignment()
    {
        var test = new CodeFixTest
        {
            TestCode = $@"{V4Stubs}{PersonStubs}
namespace TestApp
{{
    class Test
    {{
        void M()
        {{
            Person person = default;
            Person updated = person.{{|#0:WithName|}}(""Bob"");
        }}
    }}
}}",
            FixedCode = $@"{V4Stubs}{PersonStubs}
namespace TestApp
{{
    class Test
    {{
        void M()
        {{
            Person.Mutable person = default;
            person.SetName(""Bob"");
        }}
    }}
}}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ChainedWith_UnchainsToSeparateStatements()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person person = default;
            Person updated = person
                .{|#0:WithName|}(""Bob"")
                .{|#1:WithAge|}(25);
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            person.SetName(""Bob"");
            person.SetAge(25);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("WithAge", "SetAge"));

        await test.RunAsync();
    }

    [Fact]
    public async Task NestedExtractMutateReassign_CollapsesToDeepSetter()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person person = default;
            Address address = person.AddressValue;
            Address updatedAddress = address.{|#0:WithCity|}(""Manchester"");
            Person updatedPerson = person.{|#1:WithAddressValue|}(updatedAddress);
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            person.AddressValue.SetCity(""Manchester"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithCity", "SetCity"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("WithAddressValue", "SetAddressValue"));

        await test.RunAsync();
    }

    [Fact]
    public async Task InlineNestedChain_UnchainsAndRenamesBoth()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person person = default;
            Address address = person.AddressValue;
            Person result = person
                .{|#0:WithAddressValue|}(address.{|#1:WithCity|}(""Manchester""));
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            Address address = person.AddressValue;
            person.SetAddressValue(address.SetCity(""Manchester""));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithAddressValue", "SetAddressValue"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("WithCity", "SetCity"));

        await test.RunAsync();
    }

    [Fact]
    public async Task InlineNestedChainWithMultipleOuterCalls_UnchainsAll()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person person = default;
            Address address = person.AddressValue;
            Person result = person
                .{|#0:WithAddressValue|}(address.{|#1:WithCity|}(""Manchester""))
                .{|#2:WithTags|}(person.Tags.Add((Corvus.Json.JsonString)""superadmin""));
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            Address address = person.AddressValue;
            person.SetAddressValue(address.SetCity(""Manchester""));
            person.SetTags(person.Tags.Add((Corvus.Json.JsonString)""superadmin""));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,

            // Iteration 1: outer chain unchains both calls
            // Iteration 2: inner WithCity renames to SetCity
            NumberOfFixAllIterations = 2,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithAddressValue", "SetAddressValue"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("WithCity", "SetCity"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(2)
                .WithArguments("WithTags", "SetTags"));

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_CommentsInChain_PreservesComments()
    {
        // Comments between chained With calls should be preserved
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person person = default;
            person = person
                // Set the name first
                .{|#0:WithName|}(""Alice"")
                // Then the age
                .{|#1:WithAge|}(30);
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            // Set the name first
            person.SetName(""Alice"");
            // Then the age
            person.SetAge(30);
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("WithAge", "SetAge"));

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_VarType_DoesNotRewriteType()
    {
        // var should remain var; the code fix should not try to
        // change it to var.Mutable
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var person = new Person();
            person = person.{|#0:WithName|}(""test"");
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            var person = new Person();
            person.SetName(""test"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_AlreadyMutable_NoDiagnostic()
    {
        // Person.Mutable doesn't implement IJsonValue, so no diagnostic fires
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        void M()
        {
            Person.Mutable person = default;
            person.SetName(""test"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_PropertyReceiver_NoTypeRewrite()
    {
        // When the receiver is a property/field access (not a local),
        // the code fix should still rename but not try to rewrite a type.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        Person Person { get; set; }

        void M()
        {
            Person = Person.{|#0:WithName|}(""test"");
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class Test
    {
        Person Person { get; set; }

        void M()
        {
            Person.SetName(""test"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_ExpressionLambda_RenamesOnly()
    {
        // Expression lambda: p => p.WithName("test")
        // The code fix should only rename, not restructure the lambda.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    delegate Person Transform(Person p);

    class Test
    {
        void M()
        {
            Transform t = p => p.{|#0:WithName|}(""test"");
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    delegate Person Transform(Person p);

    class Test
    {
        void M()
        {
            Transform t = p => p.SetName(""test"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task WithMutation_BlockLambda_RenamesAndUnchains()
    {
        // Block lambda with assignment: code fix should work within
        // the lambda's block scope.
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    delegate Person Transform(Person p);

    class Test
    {
        void M()
        {
            Transform t = p =>
            {
                p = p.{|#0:WithName|}(""test"");
                return p;
            };
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    delegate Person Transform(Person p);

    class Test
    {
        void M()
        {
            Transform t = p =>
            {
                p.SetName(""test"");
                return p;
            };
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));

        await test.RunAsync();
    }

    [Fact]
    public async Task SetProperty_DetectedAndUnchained()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person person = default;
            Person updated = person.{|#0:SetProperty|}(""age"", default(Corvus.Json.JsonAny));
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person.Mutable person = default;
            person.SetProperty(""age"", default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("SetProperty", "SetProperty"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RemoveProperty_DetectedAndUnchained()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person person = default;
            Person updated = person.{|#0:RemoveProperty|}(""age"");
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person.Mutable person = default;
            person.RemoveProperty(""age"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("RemoveProperty", "RemoveProperty"));

        await test.RunAsync();
    }

    [Fact]
    public async Task SetProperty_ChainedWithWith_UnchainsAll()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person person = default;
            Person updated = person
                .{|#0:WithName|}(""Bob"")
                .{|#1:SetProperty|}(""extra"", default(Corvus.Json.JsonAny));
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person.Mutable person = default;
            person.SetName(""Bob"");
            person.SetProperty(""extra"", default(Corvus.Json.JsonAny));
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("WithName", "SetName"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("SetProperty", "SetProperty"));

        await test.RunAsync();
    }

    [Fact]
    public async Task SetProperty_ChainedMultiple_UnchainsAll()
    {
        var test = new CodeFixTest
        {
            TestCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person person = default;
            Person updated = person
                .{|#0:SetProperty|}(""a"", default(Corvus.Json.JsonAny))
                .{|#1:SetProperty|}(""b"", default(Corvus.Json.JsonAny))
                .{|#2:RemoveProperty|}(""c"");
        }
    }
}",
            FixedCode = V4Stubs + PersonStubs + @"
namespace TestApp
{
    class C
    {
        void M()
        {
            Person.Mutable person = default;
            person.SetProperty(""a"", default(Corvus.Json.JsonAny));
            person.SetProperty(""b"", default(Corvus.Json.JsonAny));
            person.RemoveProperty(""c"");
        }
    }
}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(0)
                .WithArguments("SetProperty", "SetProperty"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(1)
                .WithArguments("SetProperty", "SetProperty"));
        test.ExpectedDiagnostics.Add(
            Verify.Diagnostic()
                .WithLocation(2)
                .WithArguments("RemoveProperty", "RemoveProperty"));

        await test.RunAsync();
    }
}