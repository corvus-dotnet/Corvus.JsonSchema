// <copyright file="TestAssemblySetup.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// Puts the whole suite through generated code when the test run asks for it: with the environment variable
/// <c>CORVUS_TEST_CODEGEN</c> set to <c>eager</c> (or <c>warm</c>), every evaluator the tests make compiles its schema
/// before its first evaluation (or after its warm-up), whatever its options say. The suite's assertions are the
/// interpreter's results, so a run with it set checks generated code against all of them.
/// </summary>
[TestClass]
public static class TestAssemblySetup
{
    /// <summary>Reads the test run's request.</summary>
    /// <param name="context">The test context.</param>
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        JsonSchemaEvaluator.CodeGenerationOverride = Environment.GetEnvironmentVariable("CORVUS_TEST_CODEGEN") switch
        {
            "eager" => JsonSchemaCodeGeneration.Eager,
            "warm" => JsonSchemaCodeGeneration.AfterWarmUp,
            _ => null,
        };
    }
}
#endif