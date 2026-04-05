// <copyright file="JsonLogicVM.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Corvus.Numerics;
using Corvus.Runtime.InteropServices;
using Corvus.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Stack-based bytecode VM for evaluating compiled JsonLogic rules.
/// </summary>
internal static class JsonLogicVM
{
    internal static JsonElement Execute(in CompiledRule rule, in JsonElement data, JsonWorkspace workspace, bool cloneResult)
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
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) > 0);
                            break;
                        }

                    case OpCode.GreaterThanOrEqual:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) >= 0);
                            break;
                        }

                    case OpCode.LessThan:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) < 0);
                            break;
                        }

                    case OpCode.LessThanOrEqual:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) <= 0);
                            break;
                        }

                    case OpCode.Add:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticAdd(stack, sp, count, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Sub:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticSub(stack, sp, count, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Mul:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = ArithmeticMul(stack, sp, count, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Div:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = ArithmeticDiv(left, right, workspace);
                            break;
                        }

                    case OpCode.Mod:
                        {
                            JsonElement right = stack[--sp];
                            JsonElement left = stack[--sp];
                            stack[sp++] = ArithmeticMod(left, right, workspace);
                            break;
                        }

                    case OpCode.Min:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = FindMin(stack, sp, count, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Max:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = FindMax(stack, sp, count, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.Cat:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = StringCat(stack, sp, count, workspace);
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
                            stack[sp - count] = MergeArrays(stack, sp, count, workspace);
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

                            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                            {
                                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, arr.GetArrayLength());
                                JsonElement.Mutable root = doc.RootElement;

                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
                                    root.AddItem(stack[--sp]);
                                }

                                currentData = savedData;
                                stack[sp++] = root;
                            }
                            else
                            {
                                currentData = savedData;
                                stack[sp++] = JsonLogicHelpers.EmptyArray();
                            }

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

                            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                            {
                                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, arr.GetArrayLength());
                                JsonElement.Mutable root = doc.RootElement;

                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    currentData = item;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
                                    JsonElement result = stack[--sp];
                                    if (JsonLogicHelpers.IsTruthy(result))
                                    {
                                        root.AddItem(item);
                                    }
                                }

                                currentData = savedData;
                                stack[sp++] = root;
                            }
                            else
                            {
                                currentData = savedData;
                                stack[sp++] = JsonLogicHelpers.EmptyArray();
                            }

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
                                using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateObjectBuilder(workspace, 2);
                                JsonElement.Mutable root = doc.RootElement;

                                foreach (JsonElement item in arr.EnumerateArray())
                                {
                                    root.SetProperty("current", item);
                                    root.SetProperty("accumulator", accumulator);
                                    currentData = root;
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
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

                    case OpCode.MapReduceBegin:
                        {
                            sp = ExecuteMapReduceFused(bytecode, constants, stack, sp, ref pc, ref currentData, workspace);
                            break;
                        }

                    case OpCode.FilterReduceBegin:
                        {
                            sp = ExecuteFilterReduceFused(bytecode, constants, stack, sp, ref pc, ref currentData, workspace);
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
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
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
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
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
                                    sp = ExecuteBody(bytecode, constants, stack, ref sp, bodyStart, bodyLen, currentData, workspace);
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
                            stack[sp - count] = CheckMissing(stack, sp, count, currentData, workspace);
                            sp = sp - count + 1;
                            break;
                        }

                    case OpCode.MissingSome:
                        {
                            JsonElement paths = stack[--sp];
                            JsonElement needed = stack[--sp];
                            stack[sp++] = CheckMissingSome(needed, paths, currentData, workspace);
                            break;
                        }

                    case OpCode.BuildArray:
                        {
                            int count = ReadInt32(bytecode, pc);
                            pc += 4;
                            stack[sp - count] = CollectArray(stack, sp, count, workspace);
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
                            return cloneResult ? result.Clone() : result;
                        }

                    default:
                        throw new InvalidOperationException($"Unknown opcode: {op}");
                }
            }

            JsonElement finalResult = sp > 0 ? stack[--sp] : JsonLogicHelpers.NullElement();
            return cloneResult ? finalResult.Clone() : finalResult;
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
        JsonElement currentData,
        JsonWorkspace workspace)
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
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) > 0);
                        break;
                    }

                case OpCode.GreaterThanOrEqual:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) >= 0);
                        break;
                    }

                case OpCode.LessThan:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) < 0);
                        break;
                    }

                case OpCode.LessThanOrEqual:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = JsonLogicHelpers.BooleanElement(CompareCoerced(left, right, workspace) <= 0);
                        break;
                    }

                case OpCode.Add:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticAdd(stack, sp, count, workspace);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Sub:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticSub(stack, sp, count, workspace);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Mul:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = ArithmeticMul(stack, sp, count, workspace);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Div:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = ArithmeticDiv(left, right, workspace);
                        break;
                    }

                case OpCode.Mod:
                    {
                        JsonElement right = stack[--sp];
                        JsonElement left = stack[--sp];
                        stack[sp++] = ArithmeticMod(left, right, workspace);
                        break;
                    }

                case OpCode.Min:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = FindMin(stack, sp, count, workspace);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Max:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = FindMax(stack, sp, count, workspace);
                        sp = sp - count + 1;
                        break;
                    }

                case OpCode.Cat:
                    {
                        int count = ReadInt32(bytecode, pc);
                        pc += 4;
                        stack[sp - count] = StringCat(stack, sp, count, workspace);
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
                        stack[sp - count] = CollectArray(stack, sp, count, workspace);
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
            OpCode.MapReduceBegin or OpCode.FilterReduceBegin or
            OpCode.AllBegin or OpCode.NoneBegin or OpCode.SomeBegin => true,
            _ => false,
        };
    }

    private static int ExecuteMapReduceFused(
        byte[] bytecode,
        JsonElement[] constants,
        JsonElement[] stack,
        int sp,
        ref int pc,
        ref JsonElement currentData,
        JsonWorkspace workspace)
    {
        // Bytecode layout: MapReduceBegin <mapBodyLen:4> [map body] <reduceBodyLen:4> [reduce body] LoopEnd
        int mapBodyLen = ReadInt32(bytecode, pc);
        pc += 4;
        int mapBodyStart = pc;

        int reduceBodyLen = ReadInt32(bytecode, mapBodyStart + mapBodyLen);
        int reduceBodyStart = mapBodyStart + mapBodyLen + 4;

        JsonElement initialAcc = stack[--sp];
        JsonElement arr = stack[--sp];
        JsonElement savedData = currentData;
        JsonElement accumulator = initialAcc;

        if (arr.ValueKind == JsonValueKind.Array)
        {
            using JsonDocumentBuilder<JsonElement.Mutable> ctx = JsonElement.CreateObjectBuilder(workspace, 2);
            JsonElement.Mutable root = ctx.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                // Execute map body with item as data context
                currentData = item;
                sp = ExecuteBody(bytecode, constants, stack, ref sp, mapBodyStart, mapBodyLen, currentData, workspace);
                JsonElement mapped = stack[--sp];

                // Execute reduce body with {current: mapped, accumulator: acc}
                root.SetProperty("current", mapped);
                root.SetProperty("accumulator", accumulator);
                currentData = root;
                sp = ExecuteBody(bytecode, constants, stack, ref sp, reduceBodyStart, reduceBodyLen, currentData, workspace);
                accumulator = stack[--sp];
            }
        }

        currentData = savedData;
        stack[sp++] = accumulator;
        pc = reduceBodyStart + reduceBodyLen;
        if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
        {
            pc++;
        }

        return sp;
    }

    private static int ExecuteFilterReduceFused(
        byte[] bytecode,
        JsonElement[] constants,
        JsonElement[] stack,
        int sp,
        ref int pc,
        ref JsonElement currentData,
        JsonWorkspace workspace)
    {
        // Bytecode layout: FilterReduceBegin <filterBodyLen:4> [filter body] <reduceBodyLen:4> [reduce body] LoopEnd
        int filterBodyLen = ReadInt32(bytecode, pc);
        pc += 4;
        int filterBodyStart = pc;

        int reduceBodyLen = ReadInt32(bytecode, filterBodyStart + filterBodyLen);
        int reduceBodyStart = filterBodyStart + filterBodyLen + 4;

        JsonElement initialAcc = stack[--sp];
        JsonElement arr = stack[--sp];
        JsonElement savedData = currentData;
        JsonElement accumulator = initialAcc;

        if (arr.ValueKind == JsonValueKind.Array)
        {
            using JsonDocumentBuilder<JsonElement.Mutable> ctx = JsonElement.CreateObjectBuilder(workspace, 2);
            JsonElement.Mutable root = ctx.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                // Execute filter body with item as data context
                currentData = item;
                sp = ExecuteBody(bytecode, constants, stack, ref sp, filterBodyStart, filterBodyLen, currentData, workspace);
                JsonElement filterResult = stack[--sp];

                if (JsonLogicHelpers.IsTruthy(filterResult))
                {
                    // Execute reduce body with {current: item, accumulator: acc}
                    root.SetProperty("current", item);
                    root.SetProperty("accumulator", accumulator);
                    currentData = root;
                    sp = ExecuteBody(bytecode, constants, stack, ref sp, reduceBodyStart, reduceBodyLen, currentData, workspace);
                    accumulator = stack[--sp];
                }
            }
        }

        currentData = savedData;
        stack[sp++] = accumulator;
        pc = reduceBodyStart + reduceBodyLen;
        if (pc < bytecode.Length && (OpCode)bytecode[pc] == OpCode.LoopEnd)
        {
            pc++;
        }

        return sp;
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

        if (pathElement.ValueKind == JsonValueKind.Number)
        {
            // Numeric path — array index access. Get raw UTF-8 bytes (unquoted).
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathElement);
            return WalkPathUtf8(data, raw.Span);
        }

        if (pathElement.ValueKind == JsonValueKind.String)
        {
            // Get raw UTF-8 including quotes, slice off quotes for unquoted content
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathElement);
            ReadOnlySpan<byte> quoted = raw.Span;

            // Empty string "" (2 bytes) means return entire data
            if (quoted.Length <= 2)
            {
                return data;
            }

            return WalkPathUtf8(data, quoted.Slice(1, quoted.Length - 2));
        }

        return JsonLogicHelpers.NullElement();
    }

    private static JsonElement WalkPathUtf8(JsonElement current, ReadOnlySpan<byte> path)
    {
        while (path.Length > 0)
        {
            if (current.IsNullOrUndefined())
            {
                return JsonLogicHelpers.NullElement();
            }

            int dotIndex = path.IndexOf((byte)'.');
            ReadOnlySpan<byte> segment = dotIndex >= 0 ? path.Slice(0, dotIndex) : path;
            path = dotIndex >= 0 ? path.Slice(dotIndex + 1) : ReadOnlySpan<byte>.Empty;

            if (current.ValueKind == JsonValueKind.Array)
            {
                if (TryParseIndexUtf8(segment, out int index) && index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
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

    private static bool TryParseIndexUtf8(ReadOnlySpan<byte> utf8, out int value)
    {
        value = 0;
        if (utf8.Length == 0 || utf8.Length > 10)
        {
            return false;
        }

        for (int i = 0; i < utf8.Length; i++)
        {
            byte b = utf8[i];
            if (b < (byte)'0' || b > (byte)'9')
            {
                return false;
            }

            value = (value * 10) + (b - '0');
        }

        return true;
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
                ? JsonLogicHelpers.One()
                : JsonLogicHelpers.Zero();
            return CoercingEquals(leftNum, right);
        }

        if (right.ValueKind == JsonValueKind.True || right.ValueKind == JsonValueKind.False)
        {
            JsonElement rightNum = right.ValueKind == JsonValueKind.True
                ? JsonLogicHelpers.One()
                : JsonLogicHelpers.Zero();
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
            JsonValueKind.String => RawUtf8Equals(left, right),
            JsonValueKind.True or JsonValueKind.False => true, // Same ValueKind already
            JsonValueKind.Null => true,
            _ => false,
        };
    }

    private static bool RawUtf8Equals(in JsonElement left, in JsonElement right)
    {
        using RawUtf8JsonString leftRaw = JsonMarshal.GetRawUtf8Value(left);
        using RawUtf8JsonString rightRaw = JsonMarshal.GetRawUtf8Value(right);
        return leftRaw.Span.SequenceEqual(rightRaw.Span);
    }

    private static int CompareCoerced(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        // Try to coerce both to numbers for comparison
        if (JsonLogicHelpers.TryCoerceToNumber(left, workspace, out JsonElement leftNum)
            && JsonLogicHelpers.TryCoerceToNumber(right, workspace, out JsonElement rightNum))
        {
            return JsonLogicHelpers.CompareNumbers(leftNum, rightNum);
        }

        // Fall back to string comparison
        string? leftStr = JsonLogicHelpers.CoerceToString(left);
        string? rightStr = JsonLogicHelpers.CoerceToString(right);
        return string.CompareOrdinal(leftStr, rightStr);
    }

    private static bool TryCoerceToDouble(in JsonElement value, out double result)
    {
        if (JsonLogicHelpers.TryCoerceToNumber(value, out JsonElement numElement))
        {
            if (numElement.TryGetDouble(out result))
            {
                return true;
            }
        }

        result = 0;
        return false;
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

    private static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonLogicHelpers.NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        // Fallback should not happen for finite doubles
        return JsonLogicHelpers.Zero();
    }

    private static JsonElement BigNumberToElement(BigNumber value, JsonWorkspace workspace)
    {
        System.Numerics.BigInteger sig = value.Significand;
        int exp = value.Exponent;

        if (sig.IsZero)
        {
            return JsonLogicHelpers.Zero();
        }

        bool negative = sig.Sign < 0;
        System.Numerics.BigInteger absSig = System.Numerics.BigInteger.Abs(sig);

        // Format digits directly to a UTF-8 buffer on the stack
        // Max BigInteger digits we'd see in JSON numbers is ~300 chars
        byte[]? rentedArray = null;
        Span<byte> buffer = 320 <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : stackalloc byte[320];

        // Write digits to a temp area, then compose the final number
        Span<byte> digitsBuf = stackalloc byte[128];
        int digitLen = 0;

#if NET
        if (absSig.TryFormat(digitsBuf, out digitLen))
        {
            // Digits fit in stackalloc
        }
        else
#endif
        {
            // Fallback: use ToString for very large numbers (rare for JsonLogic)
            string digitsStr = absSig.ToString();
            digitLen = digitsStr.Length;
            if (digitLen > digitsBuf.Length)
            {
                rentedArray = ArrayPool<byte>.Shared.Rent(digitLen + 64);
                buffer = rentedArray;
                digitsBuf = buffer.Slice(0, digitLen);
            }

            for (int i = 0; i < digitsStr.Length; i++)
            {
                digitsBuf[i] = (byte)digitsStr[i];
            }
        }

        try
        {
            ReadOnlySpan<byte> digits = digitsBuf.Slice(0, digitLen);
            int pos = 0;

            if (negative)
            {
                buffer[pos++] = (byte)'-';
            }

            if (exp >= 0)
            {
                // Integer: digits followed by exp zeros
                digits.CopyTo(buffer.Slice(pos));
                pos += digitLen;
                for (int i = 0; i < exp; i++)
                {
                    buffer[pos++] = (byte)'0';
                }
            }
            else
            {
                int decimalPosition = digitLen + exp;
                if (decimalPosition <= 0)
                {
                    // 0.000...digits
                    buffer[pos++] = (byte)'0';
                    buffer[pos++] = (byte)'.';
                    for (int i = 0; i < -decimalPosition; i++)
                    {
                        buffer[pos++] = (byte)'0';
                    }

                    digits.CopyTo(buffer.Slice(pos));
                    pos += digitLen;
                }
                else
                {
                    // digits[0..decPos].digits[decPos..]
                    digits.Slice(0, decimalPosition).CopyTo(buffer.Slice(pos));
                    pos += decimalPosition;
                    buffer[pos++] = (byte)'.';
                    digits.Slice(decimalPosition).CopyTo(buffer.Slice(pos));
                    pos += digitLen - decimalPosition;
                }

                // Trim trailing zeros and trailing dot
                while (pos > 0 && buffer[pos - 1] == (byte)'0')
                {
                    pos--;
                }

                if (pos > 0 && buffer[pos - 1] == (byte)'.')
                {
                    pos--;
                }
            }

            return JsonLogicHelpers.NumberFromSpan(buffer.Slice(0, pos), workspace);
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    private static JsonElement ArithmeticAdd(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.Zero();
        }

        // Unary + coerces to number
        if (count == 1)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[sp - 1], workspace, out JsonElement numResult))
            {
                return numResult;
            }

            return JsonLogicHelpers.Zero();
        }

        // Fast path: try double arithmetic
        double sum = 0;
        bool allDouble = true;
        for (int i = sp - count; i < sp; i++)
        {
            if (!TryCoerceToDouble(stack[i], out double d))
            {
                allDouble = false;
                break;
            }

            sum += d;
        }

        if (allDouble)
        {
            return DoubleToElement(sum, workspace);
        }

        // Slow path: BigNumber
        BigNumber bigSum = BigNumber.Zero;
        for (int i = sp - count; i < sp; i++)
        {
            bigSum += CoerceToBigNumber(stack[i]);
        }

        return BigNumberToElement(bigSum, workspace);
    }

    private static JsonElement ArithmeticSub(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 1)
        {
            // Fast path: try double negation
            if (TryCoerceToDouble(stack[sp - 1], out double d))
            {
                return DoubleToElement(-d, workspace);
            }

            // Slow path: BigNumber negation
            BigNumber val = CoerceToBigNumber(stack[sp - 1]);
            return BigNumberToElement(-val, workspace);
        }

        if (count == 2)
        {
            // Fast path: try double subtraction
            if (TryCoerceToDouble(stack[sp - 2], out double dLeft)
                && TryCoerceToDouble(stack[sp - 1], out double dRight))
            {
                return DoubleToElement(dLeft - dRight, workspace);
            }

            // Slow path: BigNumber subtraction
            BigNumber left = CoerceToBigNumber(stack[sp - 2]);
            BigNumber right = CoerceToBigNumber(stack[sp - 1]);
            return BigNumberToElement(left - right, workspace);
        }

        return JsonLogicHelpers.Zero();
    }

    private static JsonElement ArithmeticMul(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.Zero();
        }

        // Fast path: try double arithmetic
        double product = 1;
        bool allDouble = true;
        for (int i = sp - count; i < sp; i++)
        {
            if (!TryCoerceToDouble(stack[i], out double d))
            {
                allDouble = false;
                break;
            }

            product *= d;
        }

        if (allDouble)
        {
            return DoubleToElement(product, workspace);
        }

        // Slow path: BigNumber
        BigNumber bigProduct = BigNumber.One;
        for (int i = sp - count; i < sp; i++)
        {
            bigProduct *= CoerceToBigNumber(stack[i]);
        }

        return BigNumberToElement(bigProduct, workspace);
    }

    private static JsonElement ArithmeticDiv(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        // Fast path: try double division
        if (TryCoerceToDouble(left, out double dLeft)
            && TryCoerceToDouble(right, out double dRight))
        {
            if (dRight == 0)
            {
                return JsonLogicHelpers.NullElement();
            }

            return DoubleToElement(dLeft / dRight, workspace);
        }

        // Slow path: BigNumber division
        BigNumber l = CoerceToBigNumber(left);
        BigNumber r = CoerceToBigNumber(right);

        if (r == BigNumber.Zero)
        {
            return JsonLogicHelpers.NullElement();
        }

        return BigNumberToElement(l / r, workspace);
    }

    private static JsonElement ArithmeticMod(in JsonElement left, in JsonElement right, JsonWorkspace workspace)
    {
        // Fast path: try double modulo
        if (TryCoerceToDouble(left, out double dLeft)
            && TryCoerceToDouble(right, out double dRight))
        {
            if (dRight == 0)
            {
                return JsonLogicHelpers.NullElement();
            }

            return DoubleToElement(dLeft % dRight, workspace);
        }

        // Slow path: BigNumber modulo
        BigNumber l = CoerceToBigNumber(left);
        BigNumber r = CoerceToBigNumber(right);

        if (r == BigNumber.Zero)
        {
            return JsonLogicHelpers.NullElement();
        }

        return BigNumberToElement(l % r, workspace);
    }

    private static JsonElement FindMin(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.NullElement();
        }

        JsonElement min = stack[sp - count];
        if (!JsonLogicHelpers.TryCoerceToNumber(min, workspace, out min))
        {
            return JsonLogicHelpers.NullElement();
        }

        for (int i = sp - count + 1; i < sp; i++)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[i], workspace, out JsonElement num)
                && JsonLogicHelpers.CompareNumbers(num, min) < 0)
            {
                min = num;
            }
        }

        return min;
    }

    private static JsonElement FindMax(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.NullElement();
        }

        JsonElement max = stack[sp - count];
        if (!JsonLogicHelpers.TryCoerceToNumber(max, workspace, out max))
        {
            return JsonLogicHelpers.NullElement();
        }

        for (int i = sp - count + 1; i < sp; i++)
        {
            if (JsonLogicHelpers.TryCoerceToNumber(stack[i], workspace, out JsonElement num)
                && JsonLogicHelpers.CompareNumbers(num, max) > 0)
            {
                max = num;
            }
        }

        return max;
    }

    private static JsonElement StringCat(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.EmptyString();
        }

        Utf8ValueStringBuilder builder = new(stackalloc byte[JsonConstants.StackallocByteThreshold]);
        try
        {
            // Opening quote for the JSON string value
            builder.Append((byte)'"');

            for (int i = sp - count; i < sp; i++)
            {
                JsonLogicHelpers.AppendCoercedUtf8(ref builder, stack[i]);
            }

            // Closing quote
            builder.Append((byte)'"');

            return JsonLogicHelpers.StringFromQuotedUtf8Span(builder.AsSpan(), workspace);
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static JsonElement StringSubstr(JsonElement[] stack, int sp, int argCount)
    {
        string? str = JsonLogicHelpers.CoerceToString(stack[sp - argCount]);
        if (str is null)
        {
            return JsonLogicHelpers.EmptyString();
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
            return JsonLogicHelpers.EmptyString();
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
                return JsonLogicHelpers.EmptyString();
            }

            return JsonLogicHelpers.StringToElement(str.Substring(start, length));
        }

        return JsonLogicHelpers.StringToElement(str.Substring(start));
    }

    private static bool InCheck(in JsonElement needle, in JsonElement haystack)
    {
        if (haystack.ValueKind == JsonValueKind.String)
        {
            // Use raw UTF-8 span search instead of GetString()
            using RawUtf8JsonString haystackRaw = JsonMarshal.GetRawUtf8Value(haystack);
            ReadOnlySpan<byte> haystackSpan = haystackRaw.Span;

            if (needle.ValueKind == JsonValueKind.String)
            {
                using RawUtf8JsonString needleRaw = JsonMarshal.GetRawUtf8Value(needle);
                ReadOnlySpan<byte> needleSpan = needleRaw.Span;

                // Both include quotes — slice them off for substring search
                return haystackSpan.Slice(1, haystackSpan.Length - 2)
                    .IndexOf(needleSpan.Slice(1, needleSpan.Length - 2)) >= 0;
            }

            // Non-string needle coerced to string — fall back to string-based for coercion
            string? needleStr = JsonLogicHelpers.CoerceToString(needle);
            if (needleStr is null)
            {
                return false;
            }

            // Compare coerced needle against unquoted haystack
            ReadOnlySpan<byte> unquotedHaystack = haystackSpan.Slice(1, haystackSpan.Length - 2);
            int maxByteCount = Encoding.UTF8.GetMaxByteCount(needleStr.Length);
            byte[]? rentedArray = null;
            Span<byte> needleUtf8 = maxByteCount <= JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (rentedArray = ArrayPool<byte>.Shared.Rent(maxByteCount));

            try
            {
#if NET
                int bytesWritten = Encoding.UTF8.GetBytes(needleStr, needleUtf8);
#else
                byte[] temp = Encoding.UTF8.GetBytes(needleStr);
                temp.CopyTo(needleUtf8);
                int bytesWritten = temp.Length;
#endif
                return unquotedHaystack.IndexOf(needleUtf8.Slice(0, bytesWritten)) >= 0;
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedArray);
                }
            }
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

    private static JsonElement MergeArrays(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, count * 2);
        JsonElement.Mutable root = doc.RootElement;

        for (int i = sp - count; i < sp; i++)
        {
            JsonElement val = stack[i];
            if (val.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in val.EnumerateArray())
                {
                    root.AddItem(item);
                }
            }
            else
            {
                root.AddItem(val);
            }
        }

        return root;
    }

    private static JsonElement CollectArray(JsonElement[] stack, int sp, int count, JsonWorkspace workspace)
    {
        if (count == 0)
        {
            return JsonLogicHelpers.EmptyArray();
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, count);
        JsonElement.Mutable root = doc.RootElement;
        for (int i = sp - count; i < sp; i++)
        {
            root.AddItem(stack[i]);
        }

        return root;
    }

    private static JsonElement CheckMissing(JsonElement[] stack, int sp, int count, in JsonElement data, JsonWorkspace workspace)
    {
        JsonElement.Mutable root = default;
        bool hasItems = false;

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
                        if (!hasItems)
                        {
                            root = JsonElement.CreateArrayBuilder(workspace, 4).RootElement;
                            hasItems = true;
                        }

                        root.AddItem(subPath);
                    }
                }
            }
            else
            {
                if (ResolveVar(data, pathElement).IsNullOrUndefined())
                {
                    if (!hasItems)
                    {
                        root = JsonElement.CreateArrayBuilder(workspace, 4).RootElement;
                        hasItems = true;
                    }

                    root.AddItem(pathElement);
                }
            }
        }

        return hasItems ? (JsonElement)root : JsonLogicHelpers.EmptyArray();
    }

    private static JsonElement CheckMissingSome(in JsonElement needed, in JsonElement paths, in JsonElement data, JsonWorkspace workspace)
    {
        int neededCount = 0;
        if (needed.ValueKind == JsonValueKind.Number)
        {
            BigNumber bn = CoerceToBigNumber(needed);
            neededCount = (int)(long)bn;
        }

        int totalCount = 0;
        JsonElement.Mutable root = default;
        bool hasItems = false;

        if (paths.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement p in paths.EnumerateArray())
            {
                totalCount++;
                if (ResolveVar(data, p).IsNullOrUndefined())
                {
                    if (!hasItems)
                    {
                        root = JsonElement.CreateArrayBuilder(workspace, 4).RootElement;
                        hasItems = true;
                    }

                    root.AddItem(p);
                }
            }
        }

        int missingCount = hasItems ? root.GetArrayLength() : 0;
        int present = totalCount - missingCount;
        if (present >= neededCount)
        {
            return JsonLogicHelpers.EmptyArray();
        }

        return hasItems ? (JsonElement)root : JsonLogicHelpers.EmptyArray();
    }
}