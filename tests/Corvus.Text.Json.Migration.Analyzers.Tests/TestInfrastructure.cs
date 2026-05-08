// <copyright file="TestInfrastructure.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Corvus.Text.Json.Migration.Analyzers.Tests;

/// <summary>
/// Provides <see cref="ReferenceAssemblies"/> appropriate for the current test host TFM.
/// </summary>
/// <remarks>
/// <para>
/// On .NET (net10.0), in-memory Roslyn compilations use .NET 8 reference assemblies
/// which include <c>System.Text.Json</c> natively.
/// </para>
/// <para>
/// On .NET Framework (net481), we use the .NET Framework 4.8.1 reference assemblies
/// augmented with the <c>System.Text.Json</c> NuGet package so that namespaces such as
/// <c>System.Text.Json</c> are available to the test code under analysis.
/// This mirrors the real-world scenario where .NET Framework consumers install
/// <c>System.Text.Json</c> via NuGet.
/// </para>
/// </remarks>
internal static class TestReferences
{
    /// <summary>
    /// Gets the reference assemblies for in-memory Roslyn compilations.
    /// </summary>
    public static readonly ReferenceAssemblies Assemblies =
#if NET
        ReferenceAssemblies.Net.Net80;
#else
        ReferenceAssemblies.NetFramework.Net48.Default
            .WithPackages(ImmutableArray.Create(
                new PackageIdentity("System.Text.Json", "8.0.5")));
#endif
}

/// <summary>
/// A <see cref="CSharpAnalyzerTest{TAnalyzer, TVerifier}"/> that uses
/// <see cref="TestReferences.Assemblies"/> for in-memory compilations.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
internal class AnalyzerTestBase<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzerTestBase{TAnalyzer}"/> class.
    /// </summary>
    public AnalyzerTestBase()
    {
        this.ReferenceAssemblies = TestReferences.Assemblies;
    }
}

/// <summary>
/// A <see cref="CSharpCodeFixTest{TAnalyzer, TCodeFix, TVerifier}"/> that uses
/// <see cref="TestReferences.Assemblies"/> for in-memory compilations.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
/// <typeparam name="TCodeFix">The code fix under test.</typeparam>
internal class CodeFixTestBase<TAnalyzer, TCodeFix> : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeFixTestBase{TAnalyzer, TCodeFix}"/> class.
    /// </summary>
    public CodeFixTestBase()
    {
        this.ReferenceAssemblies = TestReferences.Assemblies;
    }
}

/// <summary>
/// Wrapper around <see cref="CSharpAnalyzerVerifier{TAnalyzer, TVerifier}"/> that uses
/// <see cref="TestReferences.Assemblies"/> for in-memory compilations.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <inheritdoc cref="CSharpAnalyzerVerifier{TAnalyzer, TVerifier}.Diagnostic()"/>
    public static DiagnosticResult Diagnostic()
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic();

    /// <inheritdoc cref="CSharpAnalyzerVerifier{TAnalyzer, TVerifier}.Diagnostic(string)"/>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>
    /// Verifies that the analyzer produces the expected diagnostics for the given source.
    /// </summary>
    /// <param name="source">The test source code.</param>
    /// <param name="expected">The expected diagnostics.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new AnalyzerTestBase<TAnalyzer>
        {
            TestCode = source,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }
}
