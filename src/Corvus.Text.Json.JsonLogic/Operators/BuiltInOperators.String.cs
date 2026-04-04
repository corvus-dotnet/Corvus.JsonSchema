// <copyright file="BuiltInOperators.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    private sealed class CatOperator : IJsonLogicOperator
    {
        public string OperatorName => "cat";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            for (int i = 0; i < args.Length; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(OpCode.Cat, args.Length);
        }
    }

    private sealed class SubstrOperator : IJsonLogicOperator
    {
        public string OperatorName => "substr";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            int count = Math.Min(args.Length, 3);
            for (int i = 0; i < count; i++)
            {
                context.CompileExpression(args[i]);
            }

            context.EmitOpCodeWithOperand(OpCode.Substr, count);
        }
    }
}