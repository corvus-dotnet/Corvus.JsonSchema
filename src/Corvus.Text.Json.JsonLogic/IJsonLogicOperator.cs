// <copyright file="IJsonLogicOperator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Defines a JsonLogic operator that can compile itself into bytecode.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to provide custom operators for the
/// <see cref="JsonLogicEvaluator"/>. Register them via
/// <see cref="JsonLogicEvaluator.WithOperator(IJsonLogicOperator)"/>.
/// </para>
/// <para>
/// The <see cref="Compile"/> method is called once during rule compilation.
/// It should emit bytecode opcodes that, when executed by the VM, produce
/// the correct result on the evaluation stack.
/// </para>
/// </remarks>
public interface IJsonLogicOperator
{
    /// <summary>
    /// Gets the operator name as it appears in JsonLogic rules.
    /// </summary>
    /// <remarks>
    /// For example, <c>"var"</c>, <c>"+"</c>, or <c>"if"</c>.
    /// </remarks>
    string OperatorName { get; }

    /// <summary>
    /// Emits bytecode for this operator into the compilation context.
    /// </summary>
    /// <param name="args">
    /// The operator's arguments as <see cref="JsonElement"/> values.
    /// Each argument is a sub-expression that may itself be a rule.
    /// </param>
    /// <param name="context">
    /// The compilation context, which provides methods to emit opcodes,
    /// add constants, and recursively compile sub-expressions.
    /// </param>
    void Compile(
        ReadOnlySpan<JsonElement> args,
        IJsonLogicCompilationContext context);
}