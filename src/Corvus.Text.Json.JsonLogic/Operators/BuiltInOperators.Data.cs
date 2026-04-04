// <copyright file="BuiltInOperators.Data.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    private sealed class MissingOperator : IJsonLogicOperator
    {
        public string OperatorName => "missing";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            for (int i = 0; i < args.Length; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(OpCode.Missing, args.Length);
        }
    }

    private sealed class MissingSomeOperator : IJsonLogicOperator
    {
        public string OperatorName => "missing_some";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length >= 2)
            {
                context.CompileExpression(args[0]); // needed count
                context.CompileExpression(args[1]); // paths array
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("0"u8)));
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonElement.ParseValue("[]"u8)));
            }

            context.EmitOpCode(OpCode.MissingSome);
        }
    }
}