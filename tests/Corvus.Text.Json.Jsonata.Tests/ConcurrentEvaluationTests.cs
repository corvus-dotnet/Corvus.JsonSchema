// <copyright file="ConcurrentEvaluationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

public class ConcurrentEvaluationTests
{
    [Fact]
    public void ParallelEvaluationsOfSameExpressionWithSharedEvaluator()
    {
        Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            string json = $$"""{"price": {{i}}}""";
            using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
            using JsonWorkspace workspace = JsonWorkspace.Create();

            JsonElement result = JsonataEvaluator.Default.Evaluate(
                "price * 1.2",
                doc.RootElement,
                workspace);

            double expected = i * 1.2;
            Assert.Equal(expected, result.GetDouble(), precision: 10);
        });
    }

    [Fact]
    public void ParallelEvaluationsOfDifferentExpressionsWithSharedEvaluator()
    {
        (string Expression, Func<int, int, double> Expected)[] ops =
        [
            ("a + b", (a, b) => a + b),
            ("a * b", (a, b) => a * b),
            ("a - b", (a, b) => a - b),
        ];

        Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            var (expression, expectedFunc) = ops[i % ops.Length];
            int a = i + 1;
            int b = i + 2;

            string json = $$"""{"a": {{a}}, "b": {{b}}}""";
            using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
            using JsonWorkspace workspace = JsonWorkspace.Create();

            JsonElement result = JsonataEvaluator.Default.Evaluate(
                expression,
                doc.RootElement,
                workspace);

            Assert.Equal(expectedFunc(a, b), result.GetDouble(), precision: 10);
        });
    }

    [Fact]
    public void ParallelEvaluationsWithBindingsAreIsolated()
    {
        Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"x": 10}"""u8.ToArray());

            // Build a binding element for the multiplier value specific to this iteration.
            using var bindingDoc = ParsedJsonDocument<JsonElement>.Parse(
                Encoding.UTF8.GetBytes(i.ToString()));
            using JsonWorkspace workspace = JsonWorkspace.Create();

            var bindings = new Dictionary<string, JsonElement>
            {
                ["multiplier"] = bindingDoc.RootElement,
            };

            // Use the workspace overload so the result stays valid until we read it.
            // Bound variables are accessed with the $ prefix in JSONata.
            JsonElement result = JsonataEvaluator.Default.Evaluate(
                "x * $multiplier",
                doc.RootElement,
                workspace,
                bindings);

            Assert.Equal(10.0 * i, result.GetDouble(), precision: 10);
        });
    }

    [Fact]
    public void ConcurrentFirstCompilationCacheStampede()
    {
        // Use a fresh evaluator so the expression cache is empty.
        var evaluator = new JsonataEvaluator();
        string expression = "(x + 1) * 2";

        var tasks = new Task<double>[10];
        for (int t = 0; t < tasks.Length; t++)
        {
            int captured = t;
            tasks[t] = Task.Run(() =>
            {
                string json = $$"""{"x": {{captured}}}""";
                using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
                using JsonWorkspace workspace = JsonWorkspace.Create();

                JsonElement result = evaluator.Evaluate(
                    expression,
                    doc.RootElement,
                    workspace);

                return result.GetDouble();
            });
        }

        Task.WaitAll(tasks);

        for (int t = 0; t < tasks.Length; t++)
        {
            double expected = (t + 1) * 2;
            Assert.Equal(expected, tasks[t].Result, precision: 10);
        }
    }
}
