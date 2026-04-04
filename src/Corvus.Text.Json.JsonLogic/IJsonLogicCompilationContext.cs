// <copyright file="IJsonLogicCompilationContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Provides the context for compiling a JsonLogic rule into bytecode.
/// </summary>
/// <remarks>
/// This interface is passed to <see cref="IJsonLogicOperator.Compile"/>
/// to allow operators to emit bytecode, add constants to the constant pool,
/// and recursively compile sub-expressions.
/// </remarks>
public interface IJsonLogicCompilationContext
{
    /// <summary>
    /// Recursively compiles a sub-expression (which may be a literal or another rule).
    /// </summary>
    /// <param name="rule">The sub-expression to compile.</param>
    void CompileExpression(in JsonElement rule);

    /// <summary>
    /// Emits a single opcode with no operand.
    /// </summary>
    /// <param name="op">The opcode to emit.</param>
    void EmitOpCode(OpCode op);

    /// <summary>
    /// Emits an opcode followed by a 32-bit integer operand.
    /// </summary>
    /// <param name="op">The opcode to emit.</param>
    /// <param name="operand">The integer operand (e.g., constant pool index, jump offset, argument count).</param>
    void EmitOpCodeWithOperand(OpCode op, int operand);

    /// <summary>
    /// Adds a literal value to the constant pool and returns its index.
    /// </summary>
    /// <param name="value">The constant value to add.</param>
    /// <returns>The index of the constant in the pool.</returns>
    int AddConstant(in JsonElement value);

    /// <summary>
    /// Reserves space for a jump operand that will be patched later,
    /// and returns the position of the operand in the bytecode stream.
    /// </summary>
    /// <param name="op">The jump opcode (e.g., <see cref="OpCode.JumpIfFalsy"/>).</param>
    /// <returns>The position of the operand placeholder, for use with <see cref="PatchJump"/>.</returns>
    int EmitJumpPlaceholder(OpCode op);

    /// <summary>
    /// Patches a previously emitted jump placeholder with the correct offset.
    /// </summary>
    /// <param name="placeholderPosition">
    /// The position returned by <see cref="EmitJumpPlaceholder"/>.
    /// </param>
    void PatchJump(int placeholderPosition);

    /// <summary>
    /// Gets the current position in the bytecode stream.
    /// </summary>
    int CurrentPosition { get; }

    /// <summary>
    /// Gets a value indicating whether the last compiled expression was
    /// a constant (literal value with no <c>var</c> references).
    /// </summary>
    /// <remarks>
    /// This is used by operators to detect opportunities for constant folding.
    /// When all arguments are constants, the operator can evaluate at compile time
    /// and emit a single <see cref="OpCode.PushLiteral"/> instead.
    /// </remarks>
    bool LastExpressionWasConstant { get; }
}