// <copyright file="JsonLogicVM.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Numerics;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Stack-based bytecode VM for evaluating compiled JsonLogic rules.
/// </summary>
internal static class JsonLogicVM
{
    internal static JsonElement Execute(in CompiledRule rule, in JsonElement data)
    {
        byte[] bytecode = rule.Bytecode;
        JsonElement[] constants = rule.Constants;
        int maxDepth = Math.Max(rule.MaxStackDepth, 8);

        JsonElement[] stack = ArrayPool<JsonElement>.Shared.Rent(maxDepth);

        try
        {
            int sp = 0;
            int pc = 0;
            JsonElement currentData = data;

            while (pc < bytecode.Length)
            {
                OpCode op = (OpCode)bytecode[pc++];
                switch (op)
                {
                    case OpCode.PushLiteral:
                        {
                            int index = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp++] = constants[index];
                            break;
                        }

                    case OpCode.PushData:
                        stack[sp++] = currentData;
                        break;

                    case OpCode.Pop:
                        sp--;
                        break;

                    case OpCode.Dup:
                        stack[sp] = stack[sp - 1];
                        sp++;
                        break;

                    case OpCode.Var:
                        {
                            int pathIndex = ReadInt32(bytecode, pc);
                            pc += 4;
                            JsonElement path = constants[pathIndex];
                            stack[sp++] = ResolveVar(currentData, path);
                            break;
                        }

                    case OpCode.VarWithDefault:
                        {
                            int pathIndex = ReadInt32(bytecode, pc);
                            pc += 4;
                            JsonElement defaultVal = stack[--sp];
                            JsonElement path = constants[pathIndex];
                            JsonElement resolved = ResolveVar(currentData, path);
                            stack[sp++] = resolved.IsNullOrUndefined() ? defaultVal : resolved;
                            break;
                        }

                    case OpCode.VarDynamic:
                        {
                            JsonElement path = stack[--sp];
                            stack[sp++] = ResolveVar(currentData, path);
                            break;
                        }

                    case OpCode.VarDynamicWithDefault:
                        {
                            JsonElement defaultVal = stack[--sp];
                            JsonElement path = stack[--sp];
                            JsonElement resolved = ResolveVar(currentData, path);
                            stack[sp++] = resolved.IsNullOrUndefined() ? defaultVal : resolved;
                            break;
                        }

                    case OpCode.Equals:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CoercingEquals(left, right));
                            break;
                        }

                    case OpCode.StrictEquals:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(StrictEquals(left, right));
                            break;
                        }

                    case OpCode.NotEquals:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(!CoercingEquals(left, right));
                            break;
                        }

                    case OpCode.StrictNotEquals:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(!StrictEquals(left, right));
                            break;
                        }

                    case OpCode.Not:
                        {
                            JsonElement val = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(!JsonLogicHelpers.IsTruthy(val));
                            break;
                        }

                    case OpCode.Truthy:
                        {
                            JsonElement val = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(JsonLogicHelpers.IsTruthy(val));
                            break;
                        }

                    case OpCode.JumpIfFalsy:
                        {
                            int offset = ReadInt32(bytecode, pc);
                            pc += 4;
                            JsonElement val = stack[--sp];
                            if (!JsonLogicHelpers.IsTruthy(val))
                            {
                                pc += offset;
                            }

                            break;
                        }

                    case OpCode.JumpIfTruthy:
                        {
                            int offset = ReadInt32(bytecode, pc);
                            pc += 4;
                            JsonElement val = stack[--sp];
                            if (JsonLogicHelpers.IsTruthy(val))
                            {
                                pc += offset;
                            }

                            break;
                        }

                    case OpCode.Jump:
                        {
                            int offset = ReadInt32(bytecode, pc);
                            pc += 4;
                            pc += offset;
                            break;
                        }

                    case OpCode.GreaterThan:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) > 0);
                            break;
                        }

                    case OpCode.GreaterThanOrEqual:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) >= 0);
                            break;
                        }

                    case OpCode.LessThan:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) < 0);
                            break;
                        }

                    case OpCode.LessThanOrEqual:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) <= 0);
                            break;
                        }

                    case OpCode.Add:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticAdd(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Sub:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticSub(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Mul:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticMul(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Div:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = ArithmeticDiv(left, right);
                            break;
                        }

                    case OpCode.Mod:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = ArithmeticMod(left, right);
                            break;
                        }

                    case OpCode.Min:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = FindMin(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Max:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = FindMax(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Cat:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = StringCat(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Substr:
                        {
                            int argCount = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - argCount] = StringSubstr(stack, sp, argCount);
                            sp = sp - argCount + 1;
                            break;
                        }

                    case OpCode.In:
                        {
                            JsonElement haystack = stack[--sp];
                            JsonElement needle = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(InCheck(needle, haystack));
                            break;
                        }

                    case OpCode.Merge:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = MergeArrays(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.MapBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            var results = new List<JsonElement>();

                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    results.Add(stack[--sp]);
                                }
                            }

                            currentData = savedData;
                            stack[sp++] = BuildJsonArray(results);
                            pc = bodyStart + bodyLen;

                            // Skip past LoopEnd
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.FilterBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            var results = new List<JsonElement>();

                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    JsonElement result = stack[--sp];
                                    if (JsonLogicHelpers.IsTruthy(result))
                                    {
                                        results.Add(item);
                                    }
                                }
                            }

                            currentData = savedData;
                            stack[sp++] = BuildJsonArray(results);
                            pc = bodyStart + bodyLen;
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.ReduceBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement initialAcc = stack[--sp];
                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            JsonElement accumulator = initialAcc;

                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    // Build {"current": item, "accumulator": acc}
                                    currentData = BuildReduceContext(item, accumulator);
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    accumulator = stack[--sp];
                                }
                            }

                            currentData = savedData;
                            stack[sp++] = accumulator;
                            pc = bodyStart + bodyLen;
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.AllBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            bool result = true;

                            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    if (!JsonLogicHelpers.IsTruthy(stack[--sp]))
                                    {
                                        result = false;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Empty array → false for "all"
                                result = false;
                            }

                            currentData = savedData;
                            stack[sp++] = JsonLogicHelpers.BooleanElement(result);
                            pc = bodyStart + bodyLen;
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.NoneBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            bool result = true;

                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    if (JsonLogicHelpers.IsTruthy(stack[--sp]))
                                    {
                                        result = false;
                                        break;
                                    }
                                }
                            }

                            currentData = savedData;
                            stack[sp++] = JsonLogicHelpers.BooleanElement(result);
                            pc = bodyStart + bodyLen;
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.SomeBegin:
                        {
                            int bodyLen = ReadInt32(bytecode, pc);
                            pc += 4;
                            int bodyStart = pc;

                            JsonElement arr = stack[--sp];
                            JsonElement savedData = currentData;
                            bool result = false;

                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData);
                                    if (JsonLogicHelpers.IsTruthy(stack[--sp]))
                                    {
                                        result = true;
                                        break;
                                    }
                                }
                            }

                            currentData = savedData;
                            stack[sp++] = JsonLogicHelpers.BooleanElement(result);
                            pc = bodyStart + bodyLen;
                            if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
                            {
                                pc++;
                            }

                            break;
                        }

                    case OpCode.LoopEnd:
                        // Should not be reached during normal execution —
                        // loop opcodes skip past it.
                        break;

                    case OpCode.Missing:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = CheckMissing(stack, sp, count, currentData);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.MissingSome:
                        {
                            JsonElement paths = stack[--sp];
                            JsonElement needed = stack[--sp];
                            stack[sp++] = CheckMissingSome(needed, paths, currentData);
                            break;
                        }

                    case OpCode.BuildArray:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = CollectArray(stack, sp, count);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Log:
                        // Log is a pass-through: returns the value without consuming it.
                        // In a real implementation we'd write to a logger.
                        break;

                    case OpCode.Return:
                        {
                            JsonElement result = sp > 0 ? stack[--sp] : JsonLogicHelpers.NullElement();
                            return result;
                        }

                    default:
                        throw new InvalidOperationException($"Unknown opcode: {op}");
                }
            }

            return sp > 0 ? stack[--sp] : JsonLogicHelpers.NullElement();
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(stack);
        }
    }

    private static int ExecuteBody(
        byte[] bytecode,
        JsonElement[] constants,
        JsonElement[] stack,
        ref int sp,
        int bodyStart,
        int bodyLen,
        JsonElement currentData)
    {
        int pc = bodyStart;
        int bodyEnd = bodyStart + bodyLen;
        int savedSp = sp;

        // Re-enter the main execution loop for the body region.
        // This is a simplified recursive call — we reuse the same stack.
        // The body should push exactly one result.
        while (pc < bodyEnd)
        {
            OpCode op = (OpCode)bytecode[pc++];
            switch (op)
            {
                case OpCode.PushLiteral:
                    {
                        int index = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp++] = constants[index];
                        break;
                    }

                case OpCode.PushData:
                    stack[sp++] = currentData;
                    break;

                case OpCode.Pop:
                    sp--;
                    break;

                case OpCode.Dup:
                    stack[sp] = stack[sp - 1];
                    sp++;
                    break;

                case OpCode.Var:
                    {
                        int pathIndex = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp++] = ResolveVar(currentData, constants[pathIndex]);
                        break;
                    }

                case OpCode.VarWithDefault:
                    {
                        int pathIndex = ReadInt32(bytecode, pc);
                        pc += 4;
                        JsonElement defaultVal = stack[--sp];
                        JsonElement resolved = ResolveVar(currentData, constants[pathIndex]);
                        stack[sp++] = resolved.IsNullOrUndefined() ? defaultVal : resolved;
                        break;
                    }

                case OpCode.VarDynamic:
                    {
                        JsonElement path = stack[--sp];
                        stack[sp++] = ResolveVar(currentData, path);
                        break;
                    }

                case OpCode.VarDynamicWithDefault:
                    {
                        JsonElement defaultVal = stack[--sp];
                        JsonElement path = stack[--sp];
                        JsonElement resolved = ResolveVar(currentData, path);
                        stack[sp++] = resolved.IsNullOrUndefined() ? defaultVal : resolved;
                        break;
                    }

                case OpCode.Equals:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CoercingEquals(left, right));
                        break;
                    }

                case OpCode.StrictEquals:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(StrictEquals(left, right));
                        break;
                    }

                case OpCode.NotEquals:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(!CoercingEquals(left, right));
                        break;
                    }

                case OpCode.StrictNotEquals:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(!StrictEquals(left, right));
                        break;
                    }

                case OpCode.Not:
                    {
                        JsonElement val = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(!JsonLogicHelpers.IsTruthy(val));
                        break;
                    }

                case OpCode.Truthy:
                    {
                        JsonElement val = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(JsonLogicHelpers.IsTruthy(val));
                        break;
                    }

                case OpCode.JumpIfFalsy:
                    {
                        int offset = ReadInt32(bytecode, pc);
                        pc += 4;
                        if (!JsonLogicHelpers.IsTruthy(stack[--sp]))
                        {
                            pc += offset;
                        }

                        break;
                    }

                case OpCode.JumpIfTruthy:
                    {
                        int offset = ReadInt32(bytecode, pc);
                        pc += 4;
                        if (JsonLogicHelpers.IsTruthy(stack[--sp]))
                        {
                            pc += offset;
                        }

                        break;
                    }

                case OpCode.Jump:
                    {
                        int offset = ReadInt32(bytecode, pc);
                        pc += 4;
                        pc += offset;
                        break;
                    }

                case OpCode.GreaterThan:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) > 0);
                        break;
                    }

                case OpCode.GreaterThanOrEqual:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) >= 0);
                        break;
                    }

                case OpCode.LessThan:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) < 0);
                        break;
                    }

                case OpCode.LessThanOrEqual:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right) <= 0);
                        break;
                    }

                case OpCode.Add:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticAdd(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Sub:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticSub(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Mul:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticMul(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Div:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = ArithmeticDiv(left, right);
                        break;
                    }

                case OpCode.Mod:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = ArithmeticMod(left, right);
                        break;
                    }

                case OpCode.Min:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = FindMin(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Max:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = FindMax(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Cat:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = StringCat(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Substr:
                    {
                        int argCount = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - argCount] = StringSubstr(stack, sp, argCount);
                        sp = sp - argCount + 1;
                        break;
                    }

                case OpCode.In:
                    {
                        JsonElement haystack = stack[--sp];
                        JsonElement needle = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(InCheck(needle, haystack));
                        break;
                    }

                case OpCode.BuildArray:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = CollectArray(stack, sp, count);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Log:
                    break;

                case OpCode.Return:
                    return sp;

                default:
                    // Skip unknown opcodes with operand
                    if (HasOperand(op))
                    {
                        pc += 4;
                    }

                    break;
            }
        }

        return sp;
    }

    private static bool HasOperand(OpCode op)
    {
        return op switch
        {
            OpCode.PushLiteral or OpCode.Var or OpCode.VarWithDefault or
            OpCode.JumpIfFalsy or OpCode.JumpIfTruthy or OpCode.Jump or
            OpCode.Add or OpCode.Sub or OpCode.Mul or OpCode.Min or OpCode.Max or
            OpCode.Cat or OpCode.Substr or OpCode.Merge or OpCode.Missing or
            OpCode.BuildArray or
            OpCode.MapBegin or OpCode.FilterBegin or OpCode.ReduceBegin or
            OpCode.AllBegin or OpCode.NoneBegin or OpCode.SomeBegin => true,
            _ => false,
        };
    }

    private static int ReadInt32(byte[] bytecode, int offset)
    {
        return JsonLogicCompiler.ReadInt32(bytecode, offset);
    }

    private static JsonElement ResolveVar(in JsonElement data, in JsonElement pathElement)
    {
        // Empty string or null path means return the entire data
        if (pathElement.IsNullOrUndefined())
        {
            return data;
        }

        string? path;
        if (pathElement.ValueKind == JsonValueKind.Number)
        {
            // Numeric path — array index access
            path = JsonLogicHelpers.CoerceToString(pathElement);
        }
        else if (pathElement.ValueKind == JsonValueKind.String)
        {
            path = pathElement.GetString();
        }
        else
        {
            return JsonLogicHelpers.NullElement();
        }

        if (string.IsNullOrEmpty(path))
        {
            return data;
        }

        return WalkPath(data, path!);
    }

    private static JsonElement WalkPath(JsonElement current, string path)
    {
        string[] segments = path.Split('.');

        for (int i = 0; i < segments.Length; i++)
        {
            if (current.IsNullOrUndefined())
            {
                return JsonLogicHelpers.NullElement();
            }

            string segment = segments[i];

            if (current.ValueKind == JsonValueKind.Array)
            {
                if (int.TryParse(segment, out int index) && index >= 0 && index < current.GetArrayLength())
                {
                    int j = 0;
                    foreach (JsonElement item in current.EnumerateArray())
                    {
                        if (j == index)
                        {
                            current = item;
                            break;
                        }

                        j++;
                    }
                }
                else
                {
                    return JsonLogicHelpers.NullElement();
                }
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                if (current.TryGetProperty(segment, out JsonElement prop))
                {
                    current = prop;
                }
                else
                {
                    return JsonLogicHelpers.NullElement();
                }
            }
            else
            {
                return JsonLogicHelpers.NullElement();
            }
        }

        return current;
    }

    private static bool CoercingEquals(in JsonElement left, in JsonElement right)
    {
        // Same types — compare directly
        if (left.ValueKind == right.ValueKind)
        {
            return StrictEquals(left, right);
        }

        // Null/undefined handling
        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        // Number/string coercion
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.String)
        {
            return JsonLogicHelpers.TryCoerceToNumber(right, out JsonElement rightNum)
                && JsonLogicHelpers.AreNumbersEqual(left, rightNum);
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.Number)
        {
            return JsonLogicHelpers.TryCoerceToNumber(left, out JsonElement leftNum)
                && JsonLogicHelpers.AreNumbersEqual(leftNum, right);
        }

        // Boolean coercion: convert boolean to number, then compare
        if (left.ValueKind == JsonValueKind.True || left.ValueKind == JsonValueKind.False)
        {
            JsonElement leftNum = left.ValueKind == JsonValueKind.True
                ? JsonElement.ParseValue("1"u8)
                : JsonElement.ParseValue("0"u8);
            return CoercingEquals(leftNum, right);
        }

        if (right.ValueKind == JsonValueKind.True || right.ValueKind == JsonValueKind.False)
        {
            JsonElement rightNum = right.ValueKind == JsonValueKind.True
                ? JsonElement.ParseValue("1"u8)
                : JsonElement.ParseValue("0"u8);
            return CoercingEquals(left, rightNum);
        }

        return false;
    }

    private static bool StrictEquals(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Number => JsonLogicHelpers.AreNumbersEqual(left, right),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.True or JsonValueKind.False => true, // Same ValueKind already
            JsonValueKind.Null => true,
            _ => false,
        };
    }

    private static int CompareCoerced(in JsonElement left, in JsonElement right)
    {
        // Try to coerce both to numbers for comparison
        if (JsonLogicHelpers.TryCoerceToNumber(left, out JsonElement leftNum)
            && JsonLogicHelpers.TryCoerceToNumber(right, out JsonElement rightNum))
        {
            return JsonLogicHelpers.CompareNumbers(leftNum, rightNum);
        }

        // Fall back to string comparison
        string? leftStr = JsonLogicHelpers.CoerceToString(left);
        string? rightStr = JsonLogicHelpers.CoerceToString(right);
        return string.CompareOrdinal(leftStr, rightStr);
    }

    private static BigNumber CoerceToBigNumber(in JsonElement value)
    {
        if (JsonLogicHelpers.TryCoerceToNumber(value, out JsonElement numElement))
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(numElement);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        return BigNumber.Zero;
    }

    private static JsonElement BigNumberToElement(BigNumber value)
    {
        // Format the BigNumber as decimal notation (not scientific).
        // BigNumber = Significand × 10^Exponent
        System.Numerics.BigInteger sig = value.Significand;
        int exp = value.Exponent;

        if (sig.IsZero)
        {
            return JsonElement.ParseValue("0"u8);
        }

        bool negative = sig.Sign < 0;
        string digits = System.Numerics.BigInteger.Abs(sig).ToString();

        string result;
        if (exp >= 0)
        {
            // Integer: append zeros
            result = digits + new string('0', exp);
        }
        else
        {
            int decimalPosition = digits.Length + exp;
            if (decimalPosition <= 0)
            {
                // Need leading zeros: 0.00...digits
                result = "0." + new string('0', -decimalPosition) + digits;
            }
            else
            {
                // Insert decimal point within digits
                result = digits.Substring(0, decimalPosition) + "." + digits.Substring(decimalPosition);
            }

            // Trim trailing zeros after decimal point
            result = result.TrimEnd('0').TrimEnd('.');
        }

        if (negative)
        {
            result = "-" + result;
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(result);
        return JsonElement.ParseValue(utf8);
    }

    private static JsonElement ArithmeticAdd(JsonElement[] stack, int sp, int count)
    {
        if (count == 0)
        {
            return JsonElement.ParseValue("0"u8);
        }

        // Unary + coerces to number
        if (count == 1)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[sp - 1], out JsonElement numResult))
            {
                return numResult;
            }

            return JsonElement.ParseValue("0"u8);
        }

        BigNumber sum = BigNumber.Zero;
        for (int i = sp - count; i < sp; i++)
        {
            sum += CoerceToBigNumber(stack[i]);
        }

        return BigNumberToElement(sum);
    }

    private static JsonElement ArithmeticSub(JsonElement[] stack, int sp, int count)
    {
        if (count == 1)
        {
            // Unary negation
            BigNumber val = CoerceToBigNumber(stack[sp - 1]);
            return BigNumberToElement(-val);
        }

        if (count == 2)
        {
            BigNumber left = CoerceToBigNumber(stack[sp - 2]);
            BigNumber right = CoerceToBigNumber(stack[sp - 1]);
            return BigNumberToElement(left - right);
        }

        return JsonElement.ParseValue("0"u8);
    }

    private static JsonElement ArithmeticMul(JsonElement[] stack, int sp, int count)
    {
        if (count == 0)
        {
            return JsonElement.ParseValue("0"u8);
        }

        BigNumber product = BigNumber.One;
        for (int i = sp - count; i < sp; i++)
        {
            product *= CoerceToBigNumber(stack[i]);
        }

        return BigNumberToElement(product);
    }

    private static JsonElement ArithmeticDiv(in JsonElement left, in JsonElement right)
    {
        BigNumber l = CoerceToBigNumber(left);
        BigNumber r = CoerceToBigNumber(right);

        if (r == BigNumber.Zero)
        {
            return JsonLogicHelpers.NullElement();
        }

        return BigNumberToElement(l / r);
    }

    private static JsonElement ArithmeticMod(in JsonElement left, in JsonElement right)
    {
        BigNumber l = CoerceToBigNumber(left);
        BigNumber r = CoerceToBigNumber(right);

        if (r == BigNumber.Zero)
        {
            return JsonLogicHelpers.NullElement();
        }

        return BigNumberToElement(l % r);
    }

    private static JsonElement FindMin(JsonElement[] stack, int sp, int count)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.NullElement();
        }

        JsonElement min = stack[sp - count];
        if (!JsonLogicHelpers.TryCoerceToNumber(min, out min))
        {
            return JsonLogicHelpers.NullElement();
        }

        for (int i = sp - count + 1; i < sp; i++)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[i], out JsonElement num)
                && JsonLogicHelpers.CompareNumbers(num, min) < 0)
            {
                min = num;
            }
        }

        return min;
    }

    private static JsonElement FindMax(JsonElement[] stack, int sp, int count)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.NullElement();
        }

        JsonElement max = stack[sp - count];
        if (!JsonLogicHelpers.TryCoerceToNumber(max, out max))
        {
            return JsonLogicHelpers.NullElement();
        }

        for (int i = sp - count + 1; i < sp; i++)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[i], out JsonElement num)
                && JsonLogicHelpers.CompareNumbers(num, max) > 0)
            {
                max = num;
            }
        }

        return max;
    }

    private static JsonElement StringCat(JsonElement[] stack, int sp, int count)
    {
        if (count == 0)
        {
            return JsonElement.ParseValue("\"\""u8);
        }

        StringBuilder sb = new();
        for (int i = sp - count; i < sp; i++)
        {
            sb.Append(JsonLogicHelpers.CoerceToString(stack[i]));
        }

        string result = sb.ToString();
        return JsonLogicHelpers.StringToElement(result);
    }

    private static JsonElement StringSubstr(JsonElement[] stack, int sp, int argCount)
    {
        string? str = JsonLogicHelpers.CoerceToString(stack[sp - argCount]);
        if (str is null)
        {
            return JsonElement.ParseValue("\"\""u8);
        }

        // Get start position
        BigNumber startBn = CoerceToBigNumber(stack[sp - argCount + 1]);
        int start = (int)(long)startBn;

        // Negative start means from end
        if (start < 0)
        {
            start = Math.Max(0, str.Length + start);
        }

        if (start >= str.Length)
        {
            return JsonElement.ParseValue("\"\""u8);
        }

        if (argCount == 3)
        {
            BigNumber lenBn = CoerceToBigNumber(stack[sp - argCount + 2]);
            int length = (int)(long)lenBn;

            if (length < 0)
            {
                // Negative length means "characters from end to exclude"
                length = Math.Max(0, str.Length - start + length);
            }

            length = Math.Min(length, str.Length - start);
            if (length <= 0)
            {
                return JsonElement.ParseValue("\"\""u8);
            }

            return JsonLogicHelpers.StringToElement(str.Substring(start, length));
        }

        return JsonLogicHelpers.StringToElement(str.Substring(start));
    }

    private static bool InCheck(in JsonElement needle, in JsonElement haystack)
    {
        if (haystack.ValueKind == JsonValueKind.String)
        {
            string? haystackStr = haystack.GetString();
            string? needleStr = JsonLogicHelpers.CoerceToString(needle);
            return haystackStr != null && needleStr != null && haystackStr.Contains(needleStr);
        }

        if (haystack.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in haystack.EnumerateArray())
            {
                if (StrictEquals(needle, item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static JsonElement MergeArrays(JsonElement[] stack, int sp, int count)
    {
        List<JsonElement> merged = new();

        for (int i = sp - count; i < sp; i++)
        {
            JsonElement val = stack[i];
            if (val.ValueKind == JsonValueKind.Array)
            {
                merged.AddRange(val.EnumerateArray());
            }
            else
            {
                merged.Add(val);
            }
        }

        return BuildJsonArray(merged);
    }

    private static JsonElement CollectArray(JsonElement[] stack, int sp, int count)
    {
        List<JsonElement> items = new(count);
        for (int i = sp - count; i < sp; i++)
        {
            items.Add(stack[i]);
        }

        return BuildJsonArray(items);
    }

    private static JsonElement CheckMissing(JsonElement[] stack, int sp, int count, in JsonElement data)
    {
        List<JsonElement> missing = new();

        for (int i = sp - count; i < sp; i++)
        {
            JsonElement pathElement = stack[i];

            // Flatten arrays (missing can take arrays of paths)
            if (pathElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement subPath in pathElement.EnumerateArray())
                {
                    if (ResolveVar(data, subPath).IsNullOrUndefined())
                    {
                        missing.Add(subPath);
                    }
                }
            }
            else
            {
                if (ResolveVar(data, pathElement).IsNullOrUndefined())
                {
                    missing.Add(pathElement);
                }
            }
        }

        return BuildJsonArray(missing);
    }

    private static JsonElement CheckMissingSome(in JsonElement needed, in JsonElement paths, in JsonElement data)
    {
        int neededCount = 0;
        if (needed.ValueKind == JsonValueKind.Number)
        {
            BigNumber bn = CoerceToBigNumber(needed);
            neededCount = (int)(long)bn;
        }

        List<JsonElement> allPaths = new();
        List<JsonElement> missing = new();

        if (paths.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement p in paths.EnumerateArray())
            {
                allPaths.Add(p);
                if (ResolveVar(data, p).IsNullOrUndefined())
                {
                    missing.Add(p);
                }
            }
        }

        int present = allPaths.Count - missing.Count;
        if (present >= neededCount)
        {
            return BuildJsonArray(new List<JsonElement>());
        }

        return BuildJsonArray(missing);
    }

    private static JsonElement BuildJsonArray(List<JsonElement> items)
    {
        if (items.Count == 0)
        {
            return JsonElement.ParseValue("[]"u8);
        }

        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);
        writer.WriteStartArray();
        foreach (JsonElement item in items)
        {
            WriteElement(writer, item);
        }

        writer.WriteEndArray();
        writer.Flush();

        return JsonElement.ParseValue(ms.ToArray());
    }

    private static JsonElement BuildReduceContext(in JsonElement current, in JsonElement accumulator)
    {
        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);
        writer.WriteStartObject();
        writer.WritePropertyName("current");
        WriteElement(writer, current);
        writer.WritePropertyName("accumulator");
        WriteElement(writer, accumulator);
        writer.WriteEndObject();
        writer.Flush();

        return JsonElement.ParseValue(ms.ToArray());
    }

    private static void WriteElement(Utf8JsonWriter writer, in JsonElement element)
    {
        if (element.IsNullOrUndefined())
        {
            writer.WriteNullValue();
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty<JsonElement> prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteElement(writer, prop.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                using (RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(element))
                {
                    writer.WriteRawValue(raw.Span);
                }

                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }
    }
}