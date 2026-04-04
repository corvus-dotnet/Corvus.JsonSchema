// <copyright file="BuiltInOperators.Logic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    private sealed class IfOperator : IJsonLogicOperator
    {
        public string OperatorName => "if";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length == 0)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
                return;
            }

            if (args.Length == 1)
            {
                // Single arg: just return it
                context.CompileExpression(args[0]);
                return;
            }

            // if(cond1, then1, cond2, then2, ..., else)
            // Compile as a chain of JumpIfFalsy/Jump pairs
            List<int> endJumps = new();

            int i = 0;
            while (i < args.Length - 1)
            {
                // Compile condition
                context.CompileExpression(args[i]);
                int falsyJump = context.EmitJumpPlaceholder(OpCode.JumpIfFalsy);

                // Compile "then" branch
                context.CompileExpression(args[i + 1]);
                int endJump = context.EmitJumpPlaceholder(OpCode.Jump);
                endJumps.Add(endJump);

                // Patch the falsy jump to here
                context.PatchJump(falsyJump);

                i += 2;
            }

            // Emit the else branch (or null if no else)
            if (i < args.Length)
            {
                context.CompileExpression(args[i]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            }

            // Patch all end jumps
            foreach (int pos in endJumps)
            {
                context.PatchJump(pos);
            }
        }
    }

    private sealed class AndOperator : IJsonLogicOperator
    {
        public string OperatorName => "and";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length == 0)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.BooleanElement(false)));
                return;
            }

            // "and" returns the first falsy value, or the last value.
            // Compile: eval arg0, dup, jumpIfFalsy→END, pop, eval arg1, dup, jumpIfFalsy→END, pop, ..., eval lastArg
            // END:
            List<int> endJumps = new();

            for (int i = 0; i < args.Length - 1; i++)
            {
                context.CompileExpression(args[i]);
                context.EmitOpCode(OpCode.Dup);
                int jumpPos = context.EmitJumpPlaceholder(OpCode.JumpIfFalsy);
                endJumps.Add(jumpPos);
                context.EmitOpCode(OpCode.Pop);
            }

            // Last argument — just leave on stack
            context.CompileExpression(args[args.Length - 1]);

            // Patch all end jumps
            foreach (int pos in endJumps)
            {
                context.PatchJump(pos);
            }
        }
    }

    private sealed class OrOperator : IJsonLogicOperator
    {
        public string OperatorName => "or";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length == 0)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.BooleanElement(false)));
                return;
            }

            // "or" returns the first truthy value, or the last value.
            List<int> endJumps = new();

            for (int i = 0; i < args.Length - 1; i++)
            {
                context.CompileExpression(args[i]);
                context.EmitOpCode(OpCode.Dup);
                int jumpPos = context.EmitJumpPlaceholder(OpCode.JumpIfTruthy);
                endJumps.Add(jumpPos);
                context.EmitOpCode(OpCode.Pop);
            }

            context.CompileExpression(args[args.Length - 1]);

            foreach (int pos in endJumps)
            {
                context.PatchJump(pos);
            }
        }
    }

    private sealed class EqualsOperator : IJsonLogicOperator
    {
        public string OperatorName => "==";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            CompileBinary(args, context, OpCode.Equals);
        }
    }

    private sealed class StrictEqualsOperator : IJsonLogicOperator
    {
        public string OperatorName => "===";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            CompileBinary(args, context, OpCode.StrictEquals);
        }
    }

    private sealed class NotEqualsOperator : IJsonLogicOperator
    {
        public string OperatorName => "!=";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            CompileBinary(args, context, OpCode.NotEquals);
        }
    }

    private sealed class StrictNotEqualsOperator : IJsonLogicOperator
    {
        public string OperatorName => "!==";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            CompileBinary(args, context, OpCode.StrictNotEquals);
        }
    }

    private static void CompileBinary(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context, OpCode op)
    {
        if (args.Length >= 2)
        {
            context.CompileExpression(args[0]);
            context.CompileExpression(args[1]);
        }
        else if (args.Length == 1)
        {
            context.CompileExpression(args[0]);
            context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
        }
        else
        {
            context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
        }

        context.EmitOpCode(op);
    }
}