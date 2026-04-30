// <copyright file="PropertyNameAllocationAnalyzerTests.cs" company="Endjin Limited">
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

using AnalyzerTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
    Corvus.Text.Json.Analyzers.PropertyNameAllocationAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using CodeFixTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixTest<
    Corvus.Text.Json.Analyzers.PropertyNameAllocationAnalyzer,
    Corvus.Text.Json.Analyzers.PropertyNameAllocationCodeFix,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ008: Prefer non-allocating property name accessors.
/// </summary>
public class PropertyNameAllocationAnalyzerTests
{
    private const string Stubs = @"
using System;

namespace Corvus.Text.Json
{
    public struct JsonProperty<T>
    {
        public string Name => ""test"";
        public bool NameEquals(string text) => true;
        public bool NameEquals(ReadOnlySpan<byte> utf8Text) => true;
        public bool NameEquals(ReadOnlySpan<char> text) => true;
        public T Value => default!;
    }

    public struct JsonElement
    {
    }
}
";

    // ========================
    // Positive: == comparison
    // ========================
    [Fact]
    public async Task PropertyName_EqualityComparison_FiresCTJ008()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name == ""data""|})
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task PropertyName_InequalityComparison_FiresCTJ008()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name != ""data""|})
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task PropertyName_ReversedComparison_FiresCTJ008()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:""data"" == property.Name|})
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    // ========================
    // Positive: .Equals() call
    // ========================
    [Fact]
    public async Task PropertyName_InstanceEquals_FiresCTJ008()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name.Equals(""data"")|})
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task PropertyName_StringEquals_FiresCTJ008()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:string.Equals(property.Name, ""data"")|})
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    // ========================
    // Code fix tests
    // ========================
    [Fact]
    public async Task CodeFix_EqualityToNameEquals()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name == ""data""|})
            {
            }
        }
    }
}";

        const string fixedCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if (property.NameEquals(""data""u8))
            {
            }
        }
    }
}";

        await new CodeFixTest
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task CodeFix_InequalityToNegatedNameEquals()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name != ""data""|})
            {
            }
        }
    }
}";

        const string fixedCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if (!property.NameEquals(""data""u8))
            {
            }
        }
    }
}";

        await new CodeFixTest
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    [Fact]
    public async Task CodeFix_InstanceEqualsToNameEquals()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if ({|#0:property.Name.Equals(""data"")|})
            {
            }
        }
    }
}";

        const string fixedCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if (property.NameEquals(""data""u8))
            {
            }
        }
    }
}";

        await new CodeFixTest
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("CTJ008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("data"),
            },
        }.RunAsync();
    }

    // ========================
    // Negative tests
    // ========================
    [Fact]
    public async Task PropertyName_ComparedToVariable_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property, string name)
        {
            if (property.Name == name)
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NonJsonProperty_NameComparison_NoDiagnostic()
    {
        const string testCode = @"
namespace SomeOther
{
    public class Thing
    {
        public string Name => ""test"";
    }
}

namespace TestApp
{
    class Test
    {
        void Method(SomeOther.Thing thing)
        {
            if (thing.Name == ""data"")
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task PropertyName_UsedAsVariable_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            string n = property.Name;
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }

    [Fact]
    public async Task NameEquals_AlreadyUsed_NoDiagnostic()
    {
        const string testCode = Stubs + @"
namespace TestApp
{
    class Test
    {
        void Method(Corvus.Text.Json.JsonProperty<Corvus.Text.Json.JsonElement> property)
        {
            if (property.NameEquals(""data""))
            {
            }
        }
    }
}";

        await new AnalyzerTest
        {
            TestCode = testCode,
        }.RunAsync();
    }
}