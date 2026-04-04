// <copyright file="BuiltInOperators.Comparison.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    /// <summary>
    /// Handles comparison operators (&gt;, &gt;=, &lt;, &lt;=) including the
    /// 3-argument "between" form: <c>{"&lt;": [1, 2, 3]}</c> means <c>1 &lt; 2 &amp;&amp; 2 &lt; 3</c>.
    /// </summary>
    private sealed class ComparisonOperator : IJsonLogicOperator
    {
        private readonly OpCode opCode;

        public ComparisonOperator(OpCode opCode)
        {
            this.opCode = opCode;
        }

        public string OperatorName => this.opCode switch
        {
            OpCode.GreaterThan => ">",
            OpCode.GreaterThanOrEqual => ">=",
            OpCode.LessThan => "<",
            OpCode.LessThanOrEqual => "<=",
            _ => throw new InvalidOperationException(),
        };

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length < 2)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.BooleanElement(false)));
                return;
            }

            if (args.Length == 2)
            {
                context.CompileExpression(args[0]);
                context.CompileExpression(args[1]);
                context.EmitOpCode(this.opCode);
                return;
            }

            // Between: a op b op c → (a op b) && (b op c)
            // Compile as: eval(a), eval(b), compare, dup, jumpIfFalsy→END, pop, eval(b), eval(c), compare
            context.CompileExpression(args[0]);
            context.CompileExpression(args[1]);
            context.EmitOpCode(this.opCode);
            context.EmitOpCode(OpCode.Dup);
            int endJump = context.EmitJumpPlaceholder(OpCode.JumpIfFalsy);
            context.EmitOpCode(OpCode.Pop);
            context.CompileExpression(args[1]); // re-compile middle argument
            context.CompileExpression(args[2]);
            context.EmitOpCode(this.opCode);
            context.PatchJump(endJump);
        }
    }
}