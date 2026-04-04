// <copyright file="BuiltInOperators.Arithmetic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    /// <summary>
    /// Handles N-ary arithmetic and aggregation operators: +, *, min, max.
    /// </summary>
    private sealed class NaryArithmeticOperator : IJsonLogicOperator
    {
        private readonly OpCode opCode;

        public NaryArithmeticOperator(OpCode opCode)
        {
            this.opCode = opCode;
        }

        public string OperatorName => this.opCode switch
        {
            OpCode.Add => "+",
            OpCode.Mul => "*",
            OpCode.Min => "min",
            OpCode.Max => "max",
            _ => throw new InvalidOperationException(),
        };

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            for (int i = 0; i < args.Length; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(this.opCode, args.Length);
        }
    }

    private sealed class SubOperator : IJsonLogicOperator
    {
        public string OperatorName => "-";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length == 0)
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("0"u8)));
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(OpCode.Sub, args.Length);
        }
    }

    private sealed class DivOperator : IJsonLogicOperator
    {
        public string OperatorName => "/";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length >= 2)
            {
                context.CompileExpression(args[0]);
                context.CompileExpression(args[1]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("0"u8)));
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("1"u8)));
            }

            context.EmitOpCode(OpCode.Div);
        }
    }

    private sealed class ModOperator : IJsonLogicOperator
    {
        public string OperatorName => "%";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length >= 2)
            {
                context.CompileExpression(args[0]);
                context.CompileExpression(args[1]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("0"u8)));
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("1"u8)));
            }

            context.EmitOpCode(OpCode.Mod);
        }
    }
}