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

            // Detect reduce(map(...)) or reduce(filter(...)) and emit fused opcode
            if (TryCompileFused(args, context))
            {
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

        private static bool TryCompileFused(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            JsonElement arrayExpr = args[0];
            if (arrayExpr.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Detect {"map": [arrayExpr, mapBody]} as the array argument to reduce
            OpCode fusedOp;
            if (arrayExpr.TryGetProperty("map"u8, out JsonElement innerArgs) && innerArgs.ValueKind == JsonValueKind.Array && innerArgs.GetArrayLength() >= 2)
            {
                fusedOp = OpCode.MapReduceBegin;
            }
            else if (arrayExpr.TryGetProperty("filter"u8, out innerArgs) && innerArgs.ValueKind == JsonValueKind.Array && innerArgs.GetArrayLength() >= 2)
            {
                fusedOp = OpCode.FilterReduceBegin;
            }
            else
            {
                return false;
            }

            // Compile the shared array expression (from the inner map/filter)
            context.CompileExpression(innerArgs[0]);

            // Compile the initial accumulator
            context.CompileExpression(args[2]);

            // Emit fused opcode with placeholder for first body length
            int firstBodyPos = context.EmitJumpPlaceholder(fusedOp);

            // Compile the map/filter body
            context.CompileExpression(innerArgs[1]);

            // Patch first body length
            context.PatchJump(firstBodyPos);

            // Emit placeholder for reduce body length
            int reduceBodyPos = context.EmitOperandPlaceholder();

            // Compile the reduce body
            context.CompileExpression(args[1]);

            // Patch reduce body length
            context.PatchJump(reduceBodyPos);

            // Emit LoopEnd marker
            context.EmitOpCode(OpCode.LoopEnd);

            return true;
        }
    }
}