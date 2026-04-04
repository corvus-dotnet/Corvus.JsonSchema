// <copyright file="BuiltInOperators.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.Operators;

/// <summary>
/// Provides all standard built-in JsonLogic operators.
/// </summary>
internal static partial class BuiltInOperators
{
    internal static Dictionary<string, IJsonLogicOperator> CreateAll()
    {
        return new()
        {
            // Data access
            ["var"] = new VarOperator(),
            ["missing"] = new MissingOperator(),
            ["missing_some"] = new MissingSomeOperator(),

            // Logic
            ["if"] = new IfOperator(),
            ["?:"] = new IfOperator(),
            ["=="] = new EqualsOperator(),
            ["==="] = new StrictEqualsOperator(),
            ["!="] = new NotEqualsOperator(),
            ["!=="] = new StrictNotEqualsOperator(),
            ["!"] = new NotOperator(),
            ["!!"] = new TruthyOperator(),
            ["and"] = new AndOperator(),
            ["or"] = new OrOperator(),

            // Numeric comparison
            [">"] = new ComparisonOperator(OpCode.GreaterThan),
            [">="] = new ComparisonOperator(OpCode.GreaterThanOrEqual),
            ["<"] = new ComparisonOperator(OpCode.LessThan),
            ["<="] = new ComparisonOperator(OpCode.LessThanOrEqual),

            // Arithmetic
            ["+"] = new NaryArithmeticOperator(OpCode.Add),
            ["-"] = new SubOperator(),
            ["*"] = new NaryArithmeticOperator(OpCode.Mul),
            ["/"] = new DivOperator(),
            ["%"] = new ModOperator(),
            ["min"] = new NaryArithmeticOperator(OpCode.Min),
            ["max"] = new NaryArithmeticOperator(OpCode.Max),

            // String
            ["cat"] = new CatOperator(),
            ["substr"] = new SubstrOperator(),

            // Array
            ["in"] = new InOperator(),
            ["merge"] = new MergeOperator(),
            ["map"] = new LoopOperator(OpCode.MapBegin),
            ["filter"] = new LoopOperator(OpCode.FilterBegin),
            ["reduce"] = new ReduceOperator(),
            ["all"] = new LoopOperator(OpCode.AllBegin),
            ["none"] = new LoopOperator(OpCode.NoneBegin),
            ["some"] = new LoopOperator(OpCode.SomeBegin),

            // Misc
            ["log"] = new LogOperator(),
        };
    }

    private sealed class NotOperator : IJsonLogicOperator
    {
        public string OperatorName => "!";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length > 0)
            {
                context.CompileExpression(args[0]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            }

            context.EmitOpCode(OpCode.Not);
        }
    }

    private sealed class TruthyOperator : IJsonLogicOperator
    {
        public string OperatorName => "!!";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length > 0)
            {
                context.CompileExpression(args[0]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            }

            context.EmitOpCode(OpCode.Truthy);
        }
    }

    private sealed class LogOperator : IJsonLogicOperator
    {
        public string OperatorName => "log";

        public void Compile(ReadOnlySpan<JsonElement> args, IJsonLogicCompilationContext context)
        {
            if (args.Length > 0)
            {
                context.CompileExpression(args[0]);
            }
            else
            {
                context.EmitOpCodeWithOperand(OpCode.PushLiteral, context.AddConstant(JsonLogicHelpers.NullElement()));
            }

            context.EmitOpCode(OpCode.Log);
        }
    }
}