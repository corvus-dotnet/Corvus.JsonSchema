// <copyright file="BuiltInOperators.Array.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    private sealed class InOperator : IJsonLogicOperator
    {
        public string OperatorName => "in";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length >= 2)
            {
                context.CompileExpression(args[0]); // needle
                context.CompileExpression(args[1]); // haystack
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            }

            context.EmitOpCode(OpCode.In);
        }
    }

    private sealed class MergeOperator : IJsonLogicOperator
    {
        public string OperatorName => "merge";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            for (int i = 0; i < args.Length; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(OpCode.Merge, args.Length);
        }
    }

    /// <summary>
    /// Handles loop operators: map, filter, all, none, some.
    /// These take an array expression and a body expression.
    /// </summary>
    private sealed class LoopOperator : IJsonLogicOperator
    {
        private readonly OpCode beginOp;

        public LoopOperator(OpCode beginOp)
        {
            this.beginOp = beginOp;
        }

        public string OperatorName => this.beginOp switch
        {
            OpCode.MapBegin => "map",
            OpCode.FilterBegin => "filter",
            OpCode.AllBegin => "all",
            OpCode.NoneBegin => "none",
            OpCode.SomeBegin => "some",
            _ => throw new InvalidOperationException(),
        };

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length < 2)
            {
                // Not enough args — return empty array or false
                if (this.beginOp == OpCode.MapBegin || this.beginOp == OpCode.FilterBegin)
                {
                    context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("[]"u8)));
                }
                else
                {
                    context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.BooleanElement(false)));
                }

                return;
            }

            // Compile the array expression
            context.CompileExpression(args[0]);

            // Emit BeginOp with placeholder for body length
            int beginPos = context.EmitJumpPlaceholder(this.beginOp);

            // Compile the body expression
            context.CompileExpression(args[1]);

            // Patch the body length
            context.PatchJump(beginPos);

            // Emit LoopEnd marker
            context.EmitOpCode(OpCode.LoopEnd);
        }
    }

    private sealed class ReduceOperator : IJsonLogicOperator
    {
        public string OperatorName => "reduce";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length < 3)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
                return;
            }

            // Compile array expression
            context.CompileExpression(args[0]);

            // Compile initial accumulator
            context.CompileExpression(args[2]);

            // Emit ReduceBegin with placeholder for body length
            int beginPos = context.EmitJumpPlaceholder(OpCode.ReduceBegin);

            // Compile the body expression
            context.CompileExpression(args[1]);

            // Patch the body length
            context.PatchJump(beginPos);

            // Emit LoopEnd marker
            context.EmitOpCode(OpCode.LoopEnd);
        }
    }
}