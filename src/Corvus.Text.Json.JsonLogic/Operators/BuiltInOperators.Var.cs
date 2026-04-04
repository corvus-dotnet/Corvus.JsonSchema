// <copyright file="BuiltInOperators.Var.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

internal static partial class BuiltInOperators
{
    private sealed class VarOperator : IJsonLogicOperator
    {
        public string OperatorName => "var";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length == 0)
            {
                context.EmitOpCode(OpCode.PushData);
                return;
            }

            JsonElement pathArg = args[0];

            // Check for empty string or null — means return entire data
            if (pathArg.IsNullOrUndefined()
                || (pathArg.ValueKind == JsonValueKind.String && pathArg.GetString()?.Length == 0))
            {
                context.EmitOpCode(OpCode.PushData);
                return;
            }

            // If the path is a computed expression (operator/object), compile it dynamically
            bool isDynamic = pathArg.ValueKind == JsonValueKind.Object && pathArg.GetPropertyCount() > 0;

            if (isDynamic)
            {
                // Compile path expression — result will be on stack at runtime
                context.CompileExpression(pathArg);

                if (args.Length >= 2)
                {
                    context.CompileExpression(args[1]);
                    context.EmitOpCode(OpCode.VarDynamicWithDefault);
                }
                else
                {
                    context.EmitOpCode(OpCode.VarDynamic);
                }
            }
            else
            {
                int pathIndex = context.AddConstant(pathArg);

                if (args.Length >= 2)
                {
                    context.CompileExpression(args[1]);
                    context.EmitOpCodeWithOperand(OpCode.VarWithDefault, pathIndex);
                }
                else
                {
                    context.EmitOpCodeWithOperand(OpCode.Var, pathIndex);
                }
            }
        }
    }
}