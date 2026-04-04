// <copyright file="JsonLogicCompiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Compiles a JsonLogic rule into bytecode for evaluation by <see cref="JsonLogicVM"/>.
/// </summary>
internal sealed class JsonLogicCompiler : IJsonLogicCompilationContext
{
    private readonly Dictionary<string, IJsonLogicOperator> operators;
    private readonly List<byte> bytecode = new();
    private readonly List<JsonElement> constants = new();
    private bool lastExpressionWasConstant;

    internal JsonLogicCompiler(Dictionary<string, IJsonLogicOperator> operators)
    {
        this.operators = operators;
    }

    /// <inheritdoc/>
    public int CurrentPosition => this.bytecode.Count;

    /// <inheritdoc/>
    public bool LastExpressionWasConstant => this.lastExpressionWasConstant;

    /// <summary>
    /// Compiles a JsonLogic rule into a <see cref="CompiledRule"/>.
    /// </summary>
    internal CompiledRule Compile(in JsonElement rule)
    {
        this.CompileExpression(rule);
        this.EmitOpCode(OpCode.Return);

        byte[] bc = this.bytecode.ToArray();
        int maxDepth = CalculateMaxStackDepth(bc);

        return new CompiledRule(bc, this.constants.ToArray(), maxDepth);
    }

    /// <inheritdoc/>
    public void CompileExpression(in JsonElement rule)
    {
        if (rule.ValueKind == JsonValueKind.Object && rule.GetPropertyCount() > 0)
        {
            this.CompileOperatorCall(rule);
        }
        else if (rule.ValueKind == JsonValueKind.Array)
        {
            // Arrays in rule position evaluate each element and produce a new array.
            // This allows [1, {"var":"x"}, 3] to produce [1, <x-value>, 3].
            int count = rule.GetArrayLength();
            foreach (JsonElement item in rule.EnumerateArray())
            {
                this.CompileExpression(item);
            }

            this.EmitOpCodeWithOperand(OpCode.BuildArray, count);
            this.lastExpressionWasConstant = false;
        }
        else
        {
            this.EmitLiteral(rule);
        }
    }

    /// <inheritdoc/>
    public void EmitOpCode(OpCode op)
    {
        this.bytecode.Add((byte)op);
        this.lastExpressionWasConstant = false;
    }

    /// <inheritdoc/>
    public void EmitOpCodeWithOperand(OpCode op, int operand)
    {
        this.bytecode.Add((byte)op);
        WriteInt32(this.bytecode, operand);
        this.lastExpressionWasConstant = false;
    }

    /// <inheritdoc/>
    public int AddConstant(in JsonElement value)
    {
        int index = this.constants.Count;
        this.constants.Add(value);
        return index;
    }

    /// <inheritdoc/>
    public int EmitJumpPlaceholder(OpCode op)
    {
        this.bytecode.Add((byte)op);
        int pos = this.bytecode.Count;
        this.bytecode.Add(0);
        this.bytecode.Add(0);
        this.bytecode.Add(0);
        this.bytecode.Add(0);
        this.lastExpressionWasConstant = false;
        return pos;
    }

    /// <inheritdoc/>
    public void PatchJump(int placeholderPosition)
    {
        int offset = this.bytecode.Count - (placeholderPosition + 4);
        this.bytecode[placeholderPosition] = (byte)(offset & 0xFF);
        this.bytecode[placeholderPosition + 1] = (byte)((offset >> 8) & 0xFF);
        this.bytecode[placeholderPosition + 2] = (byte)((offset >> 16) & 0xFF);
        this.bytecode[placeholderPosition + 3] = (byte)((offset >> 24) & 0xFF);
    }

    private static void WriteInt32(List<byte> buffer, int value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)((value >> 8) & 0xFF));
        buffer.Add((byte)((value >> 16) & 0xFF));
        buffer.Add((byte)((value >> 24) & 0xFF));
    }

    internal static int ReadInt32(byte[] bytecode, int offset)
    {
        return bytecode[offset]
            | (bytecode[offset + 1] << 8)
            | (bytecode[offset + 2] << 16)
            | (bytecode[offset + 3] << 24);
    }

    private void CompileOperatorCall(in JsonElement rule)
    {
        foreach (JsonProperty<JsonElement> property in rule.EnumerateObject())
        {
            string opName = property.Name;
            JsonElement args = property.Value;

            if (!this.operators.TryGetValue(opName, out IJsonLogicOperator? op))
            {
                throw new InvalidOperationException($"Unknown JsonLogic operator: {opName}");
            }

            if (args.ValueKind == JsonValueKind.Array)
            {
                int count = args.GetArrayLength();
                JsonElement[] argArray = new JsonElement[count];
                int i = 0;
                foreach (JsonElement arg in args.EnumerateArray())
                {
                    argArray[i++] = arg;
                }

                op.Compile(argArray, this);
            }
            else
            {
                // Single argument, not wrapped in array
                op.Compile([args], this);
            }

            break; // Only process the first (and should be only) property
        }
    }

    private void EmitLiteral(in JsonElement value)
    {
        int index = this.AddConstant(value);
        this.EmitOpCodeWithOperand(OpCode.PushLiteral, index);
        this.lastExpressionWasConstant = true;
    }

    private static int CalculateMaxStackDepth(byte[] bytecode)
    {
        int current = 0;
        int max = 0;
        int pc = 0;

        while (pc < bytecode.Length)
        {
            OpCode op = (OpCode)bytecode[pc++];

            switch (op)
            {
                case OpCode.PushLiteral:
                case OpCode.Var:
                    current++;
                    pc += 4;
                    break;
                case OpCode.PushData:
                case OpCode.Dup:
                    current++;
                    break;
                case OpCode.Pop:
                    current--;
                    break;
                case OpCode.VarWithDefault:
                    // Pops default, pushes result = net 0
                    pc += 4;
                    break;
                case OpCode.Equals:
                case OpCode.StrictEquals:
                case OpCode.NotEquals:
                case OpCode.StrictNotEquals:
                case OpCode.GreaterThan:
                case OpCode.GreaterThanOrEqual:
                case OpCode.LessThan:
                case OpCode.LessThanOrEqual:
                case OpCode.Div:
                case OpCode.Mod:
                case OpCode.In:
                    // Pop 2, push 1
                    current--;
                    break;
                case OpCode.Not:
                case OpCode.Truthy:
                case OpCode.Log:
                    // Pop 1, push 1 = net 0
                    break;
                case OpCode.JumpIfFalsy:
                case OpCode.JumpIfTruthy:
                    current--;
                    pc += 4;
                    break;
                case OpCode.Jump:
                    pc += 4;
                    break;
                case OpCode.Add:
                case OpCode.Mul:
                case OpCode.Min:
                case OpCode.Max:
                case OpCode.Cat:
                case OpCode.Merge:
                case OpCode.BuildArray:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        if (count > 0)
                        {
                            current -= count - 1;
                        }

                        break;
                    }

                case OpCode.Sub:
                    {
                        int subCount = ReadInt32(bytecode, pc);
                        pc += 4;
                        if (subCount == 2)
                        {
                            current--;
                        }

                        break;
                    }

                case OpCode.Substr:
                    {
                        int argCount = ReadInt32(bytecode, pc);
                        pc += 4;
                        current -= argCount - 1;
                        break;
                    }

                case OpCode.Missing:
                    {
                        int argCount = ReadInt32(bytecode, pc);
                        pc += 4;
                        current -= argCount - 1;
                        break;
                    }

                case OpCode.MissingSome:
                    // Pops 2 (needed + paths), pushes 1 = net -1
                    current--;
                    break;

                case OpCode.AsDouble:
                case OpCode.AsLong:
                case OpCode.AsBigNumber:
                case OpCode.AsBigInteger:
                    // Pop 1, push 1 = net 0
                    break;

                case OpCode.MapBegin:
                case OpCode.FilterBegin:
                case OpCode.ReduceBegin:
                case OpCode.AllBegin:
                case OpCode.NoneBegin:
                case OpCode.SomeBegin:
                    // These pop the array and push a context; complex
                    pc += 4;
                    break;

                case OpCode.LoopEnd:
                case OpCode.Return:
                    break;
            }

            if (current > max)
            {
                max = current;
            }
        }

        // Ensure a reasonable minimum
        return Math.Max(max, 8);
    }
}