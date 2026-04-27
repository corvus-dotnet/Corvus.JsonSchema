// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Compiles JSONPath expression strings into execution plans for efficient evaluation.
/// </summary>
/// <remarks>
/// <para>
/// The compiler parses a JSONPath expression, lowers it into an optimized execution plan
/// via <see cref="Planner"/>, and wraps the result in a <see cref="CompiledJsonPath"/>
/// that can be executed against any JSON document via <see cref="PlanInterpreter"/>.
/// </para>
/// </remarks>
internal static class Compiler
{
    /// <summary>
    /// Compiles a JSONPath expression string into a <see cref="CompiledJsonPath"/>.
    /// </summary>
    /// <param name="expression">The JSONPath expression.</param>
    /// <returns>A compiled query that can be executed against JSON data.</returns>
    public static CompiledJsonPath Compile(string expression)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(expression);
        return Compile(utf8);
    }

    /// <summary>
    /// Compiles a UTF-8 JSONPath expression into a <see cref="CompiledJsonPath"/>.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded JSONPath expression.</param>
    /// <returns>A compiled query that can be executed against JSON data.</returns>
    public static CompiledJsonPath Compile(byte[] utf8Expression)
    {
        QueryNode ast = Parser.Parse(utf8Expression);
        PlanNode plan = Planner.Plan(ast);
        return new CompiledJsonPath(plan);
    }

    /// <summary>
    /// A compiled JSONPath query. Holds the execution plan and provides
    /// zero-allocation execution via <see cref="ExecuteNodes"/>.
    /// </summary>
    internal sealed class CompiledJsonPath
    {
        private readonly PlanNode _plan;

        internal CompiledJsonPath(PlanNode plan) => _plan = plan;

        /// <summary>
        /// Executes the query, writing matched nodes into <paramref name="result"/>.
        /// </summary>
        internal void ExecuteNodes(in JsonElement root, ref JsonPathResult result)
        {
            PlanInterpreter.Execute(_plan, root, ref result);
        }
    }
}
