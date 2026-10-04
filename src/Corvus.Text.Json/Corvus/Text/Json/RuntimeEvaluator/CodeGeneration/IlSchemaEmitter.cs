// <copyright file="IlSchemaEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// Writes a schema's generated methods as IL: static methods of one type in a collectible assembly, so the JIT treats
/// them as ordinary methods (it inlines between them, which it does not do between dynamic methods) and the code is
/// unloaded with the schema. The assembly reads the evaluator's internal state through
/// <c>IgnoresAccessChecksToAttribute</c>.
/// </summary>
/// <remarks>
/// A method's arguments are the evaluation state (by reference), the document and the value's row index. Constants
/// that are objects (name maps, strings, leaf resolutions) are static fields of the type, set when it is built.
/// </remarks>
internal sealed class IlSchemaEmitter : ISchemaEmitter
{
    private const int RowSize = Evaluator.RowSize;

    // The widest span of name lengths dispatched by a jump table; a wider one is a chain of comparisons.
    private const int MaxLengthTable = 64;

    // The longest name compared by words; a longer one takes the interpreter's lookup.
    private const int MaxWordsName = 128;

    private static readonly Type[] NodeParameters = [typeof(EvaluationState).MakeByRefType(), typeof(IJsonDocument), typeof(int)];

    private static readonly MethodInfo EvalNodeFast = Helper(nameof(Evaluator.EvalNodeFast));
    private static readonly MethodInfo GenToken = Helper(nameof(Evaluator.GenToken));
    private static readonly MethodInfo GenCount = Helper(nameof(Evaluator.GenCount));
    private static readonly MethodInfo GenEnd = Helper(nameof(Evaluator.GenEnd));
    private static readonly MethodInfo GenTokenAndNext = Helper(nameof(Evaluator.GenTokenAndNext));
    private static readonly MethodInfo GenName = Helper(nameof(Evaluator.GenName));
    private static readonly MethodInfo GenWord = Helper(nameof(Evaluator.GenWord));
    private static readonly MethodInfo GenSlowName = Helper(nameof(Evaluator.GenSlowName));
    private static readonly MethodInfo GenEvalGeneral = Helper(nameof(Evaluator.GenEvalGeneral));
    private static readonly MethodInfo GenNameMatches = Helper(nameof(Evaluator.GenNameMatches));
    private static readonly MethodInfo GenOwnLeaf = Helper(nameof(Evaluator.GenOwnLeaf));
    private static readonly MethodInfo GenFusedValueTests = Helper(nameof(Evaluator.GenFusedValueTests));
    private static readonly MethodInfo GenIsInteger = Helper(nameof(Evaluator.GenIsInteger));
    private static readonly MethodInfo GenStringSet = Helper(nameof(Evaluator.GenStringSet));
    private static readonly MethodInfo GenStringConst = Helper(nameof(Evaluator.GenStringConst));
    private static readonly MethodInfo GenLengthLeaf = Helper(nameof(Evaluator.GenLengthLeaf));

    private readonly TypeBuilder type;
    private readonly Dictionary<int, MethodBuilder> methods = [];
    private readonly List<(FieldBuilder Field, object Value)> constants = [];

    // The current method.
    private ILGenerator? il;
    private Label fail;

    // The current object or array: its locals, and the labels of its loop.
    private LocalBuilder? end;
    private LocalBuilder? value;
    private LocalBuilder? next;
    private LocalBuilder? token;
    private LocalBuilder? seen;
    private Label nextValue;
    private Label endOfLoop;
    private Label loop;

    // The current name dispatch: a label per name, the one for every other name, and where a case goes when it ends.
    private Label[]? cases;
    private Label otherNames;
    private Label afterCase;
    private bool casesThenCommon;

    // The node's own keywords, when more follows them: where success goes.
    private bool ownKeywords;
    private Label afterOwnKeywords;

    // The open conditional blocks (the label of the other branch, the label of the end, whether the other branch has begun).
    private readonly Stack<(Label Otherwise, Label End, bool HasOtherwise)> blocks = new();

    // The open alternatives and token switches (the label of their end).
    private readonly Stack<Label> ends = new();
    private LocalBuilder? matched;
    private LocalBuilder? count;
    private LocalBuilder? selfToken;
    private bool loopsOverProperties;

    // A fused object's state: the conditions marked failed, those that hold, those reached, the failed branches of
    // each alternative group, the kept values, and the open tries (the failure label each replaced, and its end).
    private LocalBuilder? failedConditions;
    private LocalBuilder? holds;
    private LocalBuilder? reached;
    private readonly Dictionary<int, LocalBuilder> alternativeFailures = [];
    private readonly List<LocalBuilder> valueSlots = [];
    private readonly Stack<(Label Fail, Label End)> tries = new();

    public IlSchemaEmitter()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Corvus.Text.Json.Schema." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder module = assembly.DefineDynamicModule("Schema");
        AllowAccessTo(assembly, module, typeof(Evaluator).Assembly.GetName().Name!);
        this.type = module.DefineType("Schema", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
    }

    private ILGenerator Il => this.il!;

    /// <inheritdoc/>
    public void BeginMethod(int nodeId)
    {
        this.il = this.Method(nodeId).GetILGenerator();
        this.fail = this.il.DefineLabel();
        this.token = null;
        this.matched = null;
        this.count = null;
        this.selfToken = null;
        this.ownKeywords = false;
        this.failedConditions = null;
        this.holds = null;
        this.reached = null;
        this.alternativeFailures.Clear();
        this.valueSlots.Clear();
    }

    /// <inheritdoc/>
    public void ReturnInterpreted(int nodeId)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldc_I4, nodeId);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, EvalNodeFast);
        il.Emit(OpCodes.Ret);
    }

    /// <inheritdoc/>
    public void EndMethod()
    {
        ILGenerator il = this.Il;
        il.MarkLabel(this.fail);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        this.il = null;
    }

    /// <inheritdoc/>
    public void BeginObject(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsObject, int minProperties, int maxProperties, int otherwiseInterpreted = -1, bool properties = true)
    {
        this.Prologue(JsonTokenType.StartObject, otherTokens, integerOnly, lexical, acceptsObject, minProperties, maxProperties, otherwiseInterpreted);
        this.loopsOverProperties = properties;
        if (properties)
        {
            this.BeginLoop(isObject: true);
        }
        else
        {
            this.seen = this.Il.DeclareLocal(typeof(ulong));
            this.Il.Emit(OpCodes.Ldc_I4_0);
            this.Il.Emit(OpCodes.Conv_I8);
            this.Il.Emit(OpCodes.Stloc, this.seen);
        }
    }

    /// <inheritdoc/>
    public void EndObject(ulong requiredMask)
    {
        this.EndProperties();
        this.FailUnlessSeen(requiredMask);
        this.Succeed();
    }

    /// <inheritdoc/>
    public void EndProperties()
    {
        if (this.loopsOverProperties)
        {
            this.EndLoop(isObject: true);
        }
    }

    /// <inheritdoc/>
    public void FailUnlessSeen(ulong mask)
    {
        if (mask == 0)
        {
            return;
        }

        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.seen!);
        il.Emit(OpCodes.Ldc_I8, unchecked((long)mask));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I8, unchecked((long)mask));
        il.Emit(OpCodes.Bne_Un, this.fail);
    }

    /// <inheritdoc/>
    public void BeginIfSeen(int bit)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.seen!);
        il.Emit(OpCodes.Ldc_I8, 1L << bit);
        il.Emit(OpCodes.And);
        this.BeginBlock(OpCodes.Brfalse);
    }

    /// <inheritdoc/>
    public void Succeed()
    {
        if (this.ownKeywords)
        {
            this.Il.Emit(OpCodes.Br, this.afterOwnKeywords);
        }
        else
        {
            this.Il.Emit(OpCodes.Ldc_I4_1);
            this.Il.Emit(OpCodes.Ret);
        }
    }

    /// <inheritdoc/>
    public void BeginOwnKeywords()
    {
        this.ownKeywords = true;
        this.afterOwnKeywords = this.Il.DefineLabel();
    }

    /// <inheritdoc/>
    public void EndOwnKeywords()
    {
        this.Il.MarkLabel(this.afterOwnKeywords);
        this.ownKeywords = false;
    }

    /// <inheritdoc/>
    public void FailUnlessOwnLeaf(int nodeId)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldc_I4, nodeId);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, GenOwnLeaf);
        il.Emit(OpCodes.Brfalse, this.fail);
    }

    /// <inheritdoc/>
    public void FailUnlessSelfToken(ushort tokens, bool integerOnly, bool lexical)
    {
        this.LoadSelfToken();
        this.TokenTest(tokens, integerOnly, lexical, atMethodValue: true);
    }

    /// <inheritdoc/>
    public void FailUnlessSelf(int child, bool generated)
    {
        this.CallSelf(child, generated);
        this.Il.Emit(OpCodes.Brfalse, this.fail);
    }

    /// <inheritdoc/>
    public void FailIfSelf(int child, bool generated)
    {
        this.CallSelf(child, generated);
        this.Il.Emit(OpCodes.Brtrue, this.fail);
    }

    /// <inheritdoc/>
    public void BeginIfSelf(int child, bool generated)
    {
        this.CallSelf(child, generated);
        this.BeginBlock(OpCodes.Brfalse);
    }

    /// <inheritdoc/>
    public void Else()
    {
        (Label otherwise, Label end, _) = this.blocks.Pop();
        this.Il.Emit(OpCodes.Br, end);
        this.Il.MarkLabel(otherwise);
        this.blocks.Push((otherwise, end, true));
    }

    /// <inheritdoc/>
    public void EndIf()
    {
        (Label otherwise, Label end, bool hasOtherwise) = this.blocks.Pop();
        if (!hasOtherwise)
        {
            this.Il.MarkLabel(otherwise);
        }

        this.Il.MarkLabel(end);
    }

    /// <inheritdoc/>
    public void BeginAlternatives()
    {
        this.ends.Push(this.Il.DefineLabel());
    }

    /// <inheritdoc/>
    public void OrSelf(int child, bool generated)
    {
        this.CallSelf(child, generated);
        this.Il.Emit(OpCodes.Brtrue, this.ends.Peek());
    }

    /// <inheritdoc/>
    public void EndAlternatives()
    {
        this.Il.Emit(OpCodes.Br, this.fail);
        this.Il.MarkLabel(this.ends.Pop());
    }

    /// <inheritdoc/>
    public void BeginCount()
    {
        ILGenerator il = this.Il;
        this.count ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, this.count);
    }

    /// <inheritdoc/>
    public void CountSelf(int child, bool generated)
    {
        ILGenerator il = this.Il;
        Label skip = il.DefineLabel();
        this.CallSelf(child, generated);
        il.Emit(OpCodes.Brfalse, skip);
        il.Emit(OpCodes.Ldloc, this.count!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, this.count!);
        il.Emit(OpCodes.Ldloc, this.count!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Bgt, this.fail);
        il.MarkLabel(skip);
    }

    /// <inheritdoc/>
    public void FailUnlessCountedOne()
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.count!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Bne_Un, this.fail);
    }

    /// <inheritdoc/>
    public void BeginTokenSwitch()
    {
        ILGenerator il = this.Il;
        this.selfToken ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Stloc, this.selfToken);
        this.ends.Push(il.DefineLabel());
    }

    /// <inheritdoc/>
    public void BeginTokenCase(ushort tokens)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldc_I4, (int)tokens);
        il.Emit(OpCodes.Ldloc, this.selfToken!);
        il.Emit(OpCodes.Shr);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.And);
        this.BeginBlock(OpCodes.Brfalse);
    }

    /// <inheritdoc/>
    public void EndTokenCase()
    {
        (Label otherwise, _, _) = this.blocks.Pop();
        this.Il.Emit(OpCodes.Br, this.ends.Peek());
        this.Il.MarkLabel(otherwise);
    }

    /// <inheritdoc/>
    public void EndTokenSwitch()
    {
        this.Il.MarkLabel(this.ends.Pop());
    }

    /// <inheritdoc/>
    public void ReturnInterpretedIfSeen(int bit, int nodeId)
    {
        ILGenerator il = this.Il;
        Label unseen = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, this.seen!);
        il.Emit(OpCodes.Ldc_I8, 1L << bit);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Brfalse, unseen);
        il.Emit(OpCodes.Ldc_I4, nodeId);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, GenEvalGeneral);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(unseen);
    }

    /// <inheritdoc/>
    public int DeclareValueSlot()
    {
        this.valueSlots.Add(this.Il.DeclareLocal(typeof(int)));
        return this.valueSlots.Count - 1;
    }

    /// <inheritdoc/>
    public void StoreValue(int slot)
    {
        this.Il.Emit(OpCodes.Ldloc, this.value!);
        this.Il.Emit(OpCodes.Stloc, this.valueSlots[slot]);
    }

    /// <inheritdoc/>
    public void UseStoredValue(int slot)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.valueSlots[slot]);
        il.Emit(OpCodes.Stloc, this.value!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Stloc, this.token!);
    }

    /// <inheritdoc/>
    public void FusedValueTests(FusedEntry entry, int conditions)
    {
        ILGenerator il = this.Il;
        this.failedConditions ??= il.DeclareLocal(typeof(ulong));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldloc, this.token!);
        il.Emit(OpCodes.Ldsfld, this.Constant(entry));
        il.Emit(OpCodes.Ldc_I4, conditions);
        il.Emit(OpCodes.Ldloca, this.failedConditions);
        il.Emit(OpCodes.Call, GenFusedValueTests);
    }

    /// <inheritdoc/>
    public void MarkConditionFailed(int condition)
    {
        ILGenerator il = this.Il;
        this.failedConditions ??= il.DeclareLocal(typeof(ulong));
        il.Emit(OpCodes.Ldloc, this.failedConditions);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, this.failedConditions);
    }

    /// <inheritdoc/>
    public void DecideCondition(int condition, ulong requiredMask)
    {
        // holds |= bit, unless the condition was marked failed or a required bit was not seen.
        ILGenerator il = this.Il;
        this.failedConditions ??= il.DeclareLocal(typeof(ulong));
        this.holds ??= il.DeclareLocal(typeof(ulong));
        Label not = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, this.failedConditions);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Brtrue, not);
        if (requiredMask != 0)
        {
            il.Emit(OpCodes.Ldloc, this.seen!);
            il.Emit(OpCodes.Ldc_I8, unchecked((long)requiredMask));
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Ldc_I8, unchecked((long)requiredMask));
            il.Emit(OpCodes.Bne_Un, not);
        }

        il.Emit(OpCodes.Ldloc, this.holds);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, this.holds);
        il.MarkLabel(not);
    }

    /// <inheritdoc/>
    public void DecideGate(int condition, int gate, bool gatePolarity)
    {
        // reached |= bit, when there is no gate, or the gate is reached and holds by the polarity.
        ILGenerator il = this.Il;
        this.holds ??= il.DeclareLocal(typeof(ulong));
        this.reached ??= il.DeclareLocal(typeof(ulong));
        Label not = il.DefineLabel();
        if (gate >= 0)
        {
            this.BranchUnlessActive(gate, gatePolarity, not);
        }

        il.Emit(OpCodes.Ldloc, this.reached);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, this.reached);
        il.MarkLabel(not);
    }

    /// <inheritdoc/>
    public void BeginIfActive(ReadOnlySpan<(int Condition, bool Polarity)> any)
    {
        ILGenerator il = this.Il;
        Label body = il.DefineLabel();
        Label otherwise = il.DefineLabel();
        foreach ((int condition, bool polarity) in any)
        {
            Label next = il.DefineLabel();
            this.BranchUnlessActive(condition, polarity, next);
            il.Emit(OpCodes.Br, body);
            il.MarkLabel(next);
        }

        il.Emit(OpCodes.Br, otherwise);
        il.MarkLabel(body);
        this.blocks.Push((otherwise, il.DefineLabel(), false));
    }

    /// <inheritdoc/>
    public void BeginTry()
    {
        ILGenerator il = this.Il;
        this.tries.Push((this.fail, il.DefineLabel()));
        this.fail = il.DefineLabel();
    }

    /// <inheritdoc/>
    public void OnFail()
    {
        ILGenerator il = this.Il;
        (Label outer, Label end) = this.tries.Peek();
        il.Emit(OpCodes.Br, end);
        il.MarkLabel(this.fail);
        this.fail = outer;
    }

    /// <inheritdoc/>
    public void EndTry()
    {
        this.Il.MarkLabel(this.tries.Pop().End);
    }

    /// <inheritdoc/>
    public void MarkAlternativeFailed(int group, int branch)
    {
        ILGenerator il = this.Il;
        LocalBuilder failures = this.AlternativeFailures(group);
        il.Emit(OpCodes.Ldloc, failures);
        il.Emit(OpCodes.Ldc_I8, 1L << branch);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, failures);
    }

    /// <inheritdoc/>
    public void FailUnlessAlternativeSurvives(int group, int branchCount, bool exactlyOne)
    {
        // survivors = ~failures & all; none fails, and so does more than one when exactly one must survive.
        ILGenerator il = this.Il;
        LocalBuilder survivors = il.DeclareLocal(typeof(ulong));
        il.Emit(OpCodes.Ldloc, this.AlternativeFailures(group));
        il.Emit(OpCodes.Not);
        il.Emit(OpCodes.Ldc_I8, branchCount == 64 ? -1L : (1L << branchCount) - 1);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Stloc, survivors);
        il.Emit(OpCodes.Ldloc, survivors);
        il.Emit(OpCodes.Brfalse, this.fail);
        if (exactlyOne)
        {
            il.Emit(OpCodes.Ldloc, survivors);
            il.Emit(OpCodes.Ldloc, survivors);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Conv_I8);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Brtrue, this.fail);
        }
    }

    /// <inheritdoc/>
    public void FailUnlessPropertyCount(int minProperties, int maxProperties)
    {
        if (minProperties < 0 && maxProperties < 0)
        {
            return;
        }

        ILGenerator il = this.Il;
        LocalBuilder properties = il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenCount);
        il.Emit(OpCodes.Stloc, properties);
        if (minProperties >= 0)
        {
            il.Emit(OpCodes.Ldloc, properties);
            il.Emit(OpCodes.Ldc_I4, minProperties);
            il.Emit(OpCodes.Blt, this.fail);
        }

        if (maxProperties >= 0)
        {
            il.Emit(OpCodes.Ldloc, properties);
            il.Emit(OpCodes.Ldc_I4, maxProperties);
            il.Emit(OpCodes.Bgt, this.fail);
        }
    }

    /// <inheritdoc/>
    public void FailIfSeen(ulong mask)
    {
        this.BranchIfSeen(mask, this.fail);
    }

    /// <inheritdoc/>
    public void OrSeen(ulong mask)
    {
        this.BranchIfSeen(mask, this.ends.Peek());
    }

    /// <inheritdoc/>
    public void CountSeen(ulong mask)
    {
        ILGenerator il = this.Il;
        Label counted = il.DefineLabel();
        Label skip = il.DefineLabel();
        this.BranchIfSeen(mask, counted);
        il.Emit(OpCodes.Br, skip);
        il.MarkLabel(counted);
        il.Emit(OpCodes.Ldloc, this.count!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, this.count!);
        il.Emit(OpCodes.Ldloc, this.count!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Bgt, this.fail);
        il.MarkLabel(skip);
    }

    /// <inheritdoc/>
    public void BeginPropertiesAgain()
    {
        this.loopsOverProperties = true;
        this.BeginLoop(isObject: true, again: true);
    }

    /// <inheritdoc/>
    public void SetMatched(bool matched)
    {
        ILGenerator il = this.Il;
        this.matched ??= il.DeclareLocal(typeof(bool));
        il.Emit(matched ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, this.matched);
    }

    /// <inheritdoc/>
    public void BeginIfNotMatched()
    {
        this.Il.Emit(OpCodes.Ldloc, this.matched!);
        this.BeginBlock(OpCodes.Brtrue);
    }

    /// <inheritdoc/>
    public void BeginIfNameMatches(PatternMatcher matcher)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldsfld, this.Constant(matcher));
        il.Emit(OpCodes.Call, GenNameMatches);
        this.BeginBlock(OpCodes.Brfalse);
    }

    /// <inheritdoc/>
    public void BeginArray(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems)
    {
        this.Prologue(JsonTokenType.StartArray, otherTokens, integerOnly, lexical, acceptsArray, minItems, maxItems);
        this.BeginLoop(isObject: false);
    }

    /// <inheritdoc/>
    public void EndArray()
    {
        this.EndLoop(isObject: false);
        this.Succeed();
    }

    /// <inheritdoc/>
    public void ReturnArrayWithoutItems(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems)
    {
        this.Prologue(JsonTokenType.StartArray, otherTokens, integerOnly, lexical, acceptsArray, minItems, maxItems);
        this.Succeed();
    }

    /// <inheritdoc/>
    public void ReturnTokenTest(ushort tokens, bool integerOnly, bool lexical)
    {
        this.LoadSelfToken();
        this.TokenTest(tokens, integerOnly, lexical, atMethodValue: true);
        this.Succeed();
    }

    /// <inheritdoc/>
    public void ReturnChildByToken(int[] childByToken, bool[] generatedByToken)
    {
        ILGenerator il = this.Il;
        var labels = new Label[childByToken.Length];
        var byChild = new Dictionary<int, Label>();
        for (int i = 0; i < childByToken.Length; i++)
        {
            if (childByToken[i] < 0)
            {
                labels[i] = this.fail;
            }
            else if (!byChild.TryGetValue(childByToken[i], out labels[i]))
            {
                byChild[childByToken[i]] = labels[i] = il.DefineLabel();
            }
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Switch, labels);
        il.Emit(OpCodes.Br, this.fail);
        for (int i = 0; i < childByToken.Length; i++)
        {
            if (childByToken[i] >= 0 && byChild.Remove(childByToken[i], out Label label))
            {
                il.MarkLabel(label);
                if (generatedByToken[i])
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Call, this.Method(childByToken[i]));
                }
                else
                {
                    il.Emit(OpCodes.Ldc_I4, childByToken[i]);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, EvalNodeFast);
                }

                il.Emit(OpCodes.Ret);
            }
        }
    }

    /// <inheritdoc/>
    public void BeginNameDispatch<T>(Utf8NameMap<T> names, bool thenCommon = false)
        where T : class
    {
        ILGenerator il = this.Il;
        this.casesThenCommon = thenCommon;
        this.afterCase = thenCommon ? il.DefineLabel() : this.nextValue;
        byte[][] keys = names.Keys;
        this.cases = new Label[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            this.cases[i] = il.DefineLabel();
        }

        this.otherNames = il.DefineLabel();
        Label slow = il.DefineLabel();
        LocalBuilder location = il.DeclareLocal(typeof(int));
        LocalBuilder length = il.DeclareLocal(typeof(int));
        LocalBuilder first = il.DeclareLocal(typeof(ulong));
        LocalBuilder masked = il.DeclareLocal(typeof(ulong));

        // The names by length; within a length, by words: one masked word up to 8 bytes, and beyond that each 8 bytes
        // in turn with the last 8 overlapping (the interpreter's lookup for a name longer than MaxWordsName).
        var byLength = new SortedDictionary<int, List<int>>();
        for (int i = 0; i < keys.Length; i++)
        {
            if (!byLength.TryGetValue(keys[i].Length, out List<int>? sameLength))
            {
                byLength[keys[i].Length] = sameLength = [];
            }

            sameLength.Add(i);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldloca, length);
        il.Emit(OpCodes.Call, GenName);
        il.Emit(OpCodes.Stloc, location);
        il.Emit(OpCodes.Ldloc, location);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Blt, slow);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, location);
        il.Emit(OpCodes.Call, GenWord);
        il.Emit(OpCodes.Stloc, first);

        var lengthLabels = new Dictionary<int, Label>();
        foreach (int nameLength in byLength.Keys)
        {
            lengthLabels[nameLength] = nameLength > MaxWordsName ? slow : il.DefineLabel();
        }

        if (byLength.Count > 0)
        {
            int shortest = int.MaxValue;
            int longest = 0;
            foreach (int nameLength in byLength.Keys)
            {
                shortest = Math.Min(shortest, nameLength);
                longest = Math.Max(longest, nameLength);
            }

            if (longest - shortest < MaxLengthTable)
            {
                var table = new Label[longest - shortest + 1];
                for (int i = 0; i < table.Length; i++)
                {
                    table[i] = lengthLabels.TryGetValue(shortest + i, out Label label) ? label : this.otherNames;
                }

                il.Emit(OpCodes.Ldloc, length);
                il.Emit(OpCodes.Ldc_I4, shortest);
                il.Emit(OpCodes.Sub);
                il.Emit(OpCodes.Switch, table);
            }
            else
            {
                foreach (KeyValuePair<int, Label> label in lengthLabels)
                {
                    il.Emit(OpCodes.Ldloc, length);
                    il.Emit(OpCodes.Ldc_I4, label.Key);
                    il.Emit(OpCodes.Beq, label.Value);
                }
            }
        }

        il.Emit(OpCodes.Br, this.otherNames);

        Span<byte> padded = stackalloc byte[sizeof(ulong)];
        foreach (KeyValuePair<int, List<int>> sameLength in byLength)
        {
            int nameLength = sameLength.Key;
            if (nameLength > MaxWordsName)
            {
                continue;
            }

            il.MarkLabel(lengthLabels[nameLength]);
            if (nameLength <= sizeof(ulong))
            {
                // The word read may run past the name: mask it to the name's bytes.
                il.Emit(OpCodes.Ldloc, first);
                if (nameLength < sizeof(ulong))
                {
                    padded.Clear();
                    padded.Slice(0, nameLength).Fill(0xFF);
                    il.Emit(OpCodes.Ldc_I8, unchecked((long)MemoryMarshal.Read<ulong>(padded)));
                    il.Emit(OpCodes.And);
                }

                il.Emit(OpCodes.Stloc, masked);
                foreach (int index in sameLength.Value)
                {
                    padded.Clear();
                    keys[index].CopyTo(padded);
                    il.Emit(OpCodes.Ldloc, masked);
                    il.Emit(OpCodes.Ldc_I8, unchecked((long)MemoryMarshal.Read<ulong>(padded)));
                    il.Emit(OpCodes.Beq, this.cases[index]);
                }
            }
            else
            {
                // A mismatch is nearly always in the first word, which is already read.
                foreach (int index in sameLength.Value)
                {
                    Label differs = il.DefineLabel();
                    il.Emit(OpCodes.Ldloc, first);
                    il.Emit(OpCodes.Ldc_I8, unchecked((long)MemoryMarshal.Read<ulong>(keys[index])));
                    il.Emit(OpCodes.Bne_Un, differs);
                    for (int offset = sizeof(ulong); offset < nameLength; offset += sizeof(ulong))
                    {
                        int at = Math.Min(offset, nameLength - sizeof(ulong));
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldloc, location);
                        il.Emit(OpCodes.Ldc_I4, at);
                        il.Emit(OpCodes.Add);
                        il.Emit(OpCodes.Call, GenWord);
                        il.Emit(OpCodes.Ldc_I8, unchecked((long)MemoryMarshal.Read<ulong>(keys[index].AsSpan(at))));
                        il.Emit(OpCodes.Bne_Un, differs);
                    }

                    il.Emit(OpCodes.Br, this.cases[index]);
                    il.MarkLabel(differs);
                }
            }

            il.Emit(OpCodes.Br, this.otherNames);
        }

        // Escaped names, names at the very end of the text and very long names: the interpreter's lookup, then the case.
        il.MarkLabel(slow);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldsfld, this.Constant(names));
        il.Emit(OpCodes.Call, GenSlowName.MakeGenericMethod(typeof(T)));
        if (keys.Length > 0)
        {
            il.Emit(OpCodes.Switch, this.cases);
        }
        else
        {
            il.Emit(OpCodes.Pop);
        }

        il.Emit(OpCodes.Br, this.otherNames);
    }

    /// <inheritdoc/>
    public void BeginCase(int index)
    {
        this.Il.MarkLabel(index < 0 ? this.otherNames : this.cases![index]);
    }

    /// <inheritdoc/>
    public void EndCase()
    {
        this.Il.Emit(OpCodes.Br, this.afterCase);
    }

    /// <inheritdoc/>
    public void EndNameDispatch()
    {
        if (this.casesThenCommon)
        {
            this.Il.MarkLabel(this.afterCase);
        }

        this.cases = null;
    }

    /// <inheritdoc/>
    public void MarkSeen(int bit)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.seen!);
        il.Emit(OpCodes.Ldc_I8, 1L << bit);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Stloc, this.seen!);
    }

    /// <inheritdoc/>
    public void Fail()
    {
        this.Il.Emit(OpCodes.Br, this.fail);
    }

    /// <inheritdoc/>
    public void FailUnlessToken(ushort tokens, bool integerOnly, bool lexical)
    {
        this.TokenTest(tokens, integerOnly, lexical, atMethodValue: false);
    }

    /// <inheritdoc/>
    public void FailUnlessStringSet(Utf8NameMap<object> allowed)
    {
        this.ValueTest(GenStringSet, this.Constant(allowed), byAddress: false);
    }

    /// <inheritdoc/>
    public void FailUnlessStringConst(byte[] expected)
    {
        this.ValueTest(GenStringConst, this.Constant(expected), byAddress: false);
    }

    /// <inheritdoc/>
    public void FailUnlessLength(in StrictEntry entry)
    {
        this.ValueTest(GenLengthLeaf, this.Constant(entry), byAddress: true);
    }

    /// <inheritdoc/>
    public void FailUnlessChild(ushort decided, ushort accepts, int child, bool generated)
    {
        ILGenerator il = this.Il;
        Label done = il.DefineLabel();
        if (decided != 0)
        {
            // A token type the child's type decides: accepted or not in place.
            Label call = il.DefineLabel();
            this.TokenBit(decided);
            il.Emit(OpCodes.Brfalse, call);
            if ((accepts & decided) == 0)
            {
                il.Emit(OpCodes.Br, this.fail);
            }
            else
            {
                if ((accepts & decided) != decided)
                {
                    this.TokenBit(accepts);
                    il.Emit(OpCodes.Brfalse, this.fail);
                }

                il.Emit(OpCodes.Br, done);
            }

            il.MarkLabel(call);
        }

        if (generated)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, this.value!);
            il.Emit(OpCodes.Call, this.Method(child));
        }
        else
        {
            il.Emit(OpCodes.Ldc_I4, child);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, this.value!);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, EvalNodeFast);
        }

        il.Emit(OpCodes.Brfalse, this.fail);
        il.MarkLabel(done);
    }

    /// <summary>Creates the type, sets its constants, compiles its methods and returns them by node.</summary>
    /// <returns>Each node's method.</returns>
    public Dictionary<int, NodeValidator> Build()
    {
        Type created = this.type.CreateType();
        foreach ((FieldBuilder field, object constant) in this.constants)
        {
            created.GetField(field.Name)!.SetValue(null, constant);
        }

        // Compiled here, on the thread that builds the schema's code (tiering's background task), not at each
        // method's first call on a thread that is evaluating.
        foreach (MethodBuilder method in this.methods.Values)
        {
            RuntimeHelpers.PrepareMethod(created.GetMethod(method.Name)!.MethodHandle);
        }

        var built = new Dictionary<int, NodeValidator>(this.methods.Count);
        foreach (KeyValuePair<int, MethodBuilder> method in this.methods)
        {
            built[method.Key] = created.GetMethod(method.Value.Name)!.CreateDelegate<NodeValidator>();
        }

        return built;
    }

    // The method's value: a container of the kind goes on (unless its type or count rejects it); anything else is
    // decided by its token type.
    private void Prologue(JsonTokenType container, ushort otherTokens, bool integerOnly, bool lexical, bool acceptsContainer, int minCount, int maxCount, int otherwiseInterpreted = -1)
    {
        ILGenerator il = this.Il;
        this.token ??= il.DeclareLocal(typeof(int));
        Label isContainer = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Stloc, this.token);
        il.Emit(OpCodes.Ldloc, this.token);
        il.Emit(OpCodes.Ldc_I4, (int)container);
        il.Emit(OpCodes.Beq, isContainer);
        if (otherwiseInterpreted >= 0)
        {
            il.Emit(OpCodes.Ldc_I4, otherwiseInterpreted);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, GenEvalGeneral);
            il.Emit(OpCodes.Brfalse, this.fail);
        }
        else if (otherTokens != ushort.MaxValue)
        {
            this.TokenTest(otherTokens, integerOnly, lexical, atMethodValue: true);
        }

        this.Succeed();

        il.MarkLabel(isContainer);
        if (!acceptsContainer)
        {
            il.Emit(OpCodes.Br, this.fail);
        }

        if (minCount >= 0 || maxCount >= 0)
        {
            LocalBuilder count = il.DeclareLocal(typeof(int));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, GenCount);
            il.Emit(OpCodes.Stloc, count);
            if (minCount >= 0)
            {
                il.Emit(OpCodes.Ldloc, count);
                il.Emit(OpCodes.Ldc_I4, minCount);
                il.Emit(OpCodes.Blt, this.fail);
            }

            if (maxCount >= 0)
            {
                il.Emit(OpCodes.Ldloc, count);
                il.Emit(OpCodes.Ldc_I4, maxCount);
                il.Emit(OpCodes.Bgt, this.fail);
            }
        }
    }

    // The loop over a container's values. An object's rows are name, value, name, value: the first value is two rows
    // in, the loop runs while the value's name row is before the end, and the next value is a row past the next
    // name. An array's are its items: the first is one row in, and the next is the row after the item.
    private void BeginLoop(bool isObject, bool again = false)
    {
        ILGenerator il = this.Il;
        this.end = il.DeclareLocal(typeof(int));
        this.value = il.DeclareLocal(typeof(int));
        this.next = il.DeclareLocal(typeof(int));
        this.nextValue = il.DefineLabel();
        this.endOfLoop = il.DefineLabel();
        this.loop = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenEnd);
        il.Emit(OpCodes.Stloc, this.end);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4, isObject ? 2 * RowSize : RowSize);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, this.value);
        if (isObject && !again)
        {
            this.seen = il.DeclareLocal(typeof(ulong));
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_I8);
            il.Emit(OpCodes.Stloc, this.seen);
        }

        il.MarkLabel(this.loop);
        il.Emit(OpCodes.Ldloc, this.value);
        if (isObject)
        {
            il.Emit(OpCodes.Ldc_I4, RowSize);
            il.Emit(OpCodes.Sub);
        }

        il.Emit(OpCodes.Ldloc, this.end);
        il.Emit(OpCodes.Bge, this.endOfLoop);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value);
        il.Emit(OpCodes.Ldloca, this.next);
        il.Emit(OpCodes.Call, GenTokenAndNext);
        il.Emit(OpCodes.Stloc, this.token!);
    }

    private void EndLoop(bool isObject)
    {
        ILGenerator il = this.Il;
        il.MarkLabel(this.nextValue);
        il.Emit(OpCodes.Ldloc, this.next!);
        if (isObject)
        {
            il.Emit(OpCodes.Ldc_I4, RowSize);
            il.Emit(OpCodes.Add);
        }

        il.Emit(OpCodes.Stloc, this.value!);
        il.Emit(OpCodes.Br, this.loop);
        il.MarkLabel(this.endOfLoop);
    }

    private LocalBuilder AlternativeFailures(int group)
    {
        if (!this.alternativeFailures.TryGetValue(group, out LocalBuilder? failures))
        {
            this.alternativeFailures[group] = failures = this.Il.DeclareLocal(typeof(ulong));
        }

        return failures;
    }

    // Branches when every bit of a mask was marked seen (always, for no bits).
    private void BranchIfSeen(ulong mask, Label target)
    {
        ILGenerator il = this.Il;
        if (mask == 0)
        {
            il.Emit(OpCodes.Br, target);
            return;
        }

        il.Emit(OpCodes.Ldloc, this.seen!);
        il.Emit(OpCodes.Ldc_I8, unchecked((long)mask));
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ldc_I8, unchecked((long)mask));
        il.Emit(OpCodes.Beq, target);
    }

    // Branches unless a condition is reached and holds (or does not hold) as given.
    private void BranchUnlessActive(int condition, bool polarity, Label target)
    {
        ILGenerator il = this.Il;
        this.holds ??= il.DeclareLocal(typeof(ulong));
        this.reached ??= il.DeclareLocal(typeof(ulong));
        il.Emit(OpCodes.Ldloc, this.reached);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Brfalse, target);
        il.Emit(OpCodes.Ldloc, this.holds);
        il.Emit(OpCodes.Ldc_I8, 1L << condition);
        il.Emit(OpCodes.And);
        il.Emit(polarity ? OpCodes.Brfalse : OpCodes.Brtrue, target);
    }

    // Reads the method's value's token type into the token local (a loop over its values overwrites it).
    private void LoadSelfToken()
    {
        ILGenerator il = this.Il;
        this.token ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Stloc, this.token);
    }

    // Pushes whether the method's value is valid against a child node.
    private void CallSelf(int child, bool generated)
    {
        ILGenerator il = this.Il;
        if (generated)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, this.Method(child));
        }
        else
        {
            il.Emit(OpCodes.Ldc_I4, child);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, EvalNodeFast);
        }
    }

    // Opens a conditional block: the value on the stack sends control to the block's other branch by the given branch.
    private void BeginBlock(OpCode toOtherwise)
    {
        ILGenerator il = this.Il;
        Label otherwise = il.DefineLabel();
        il.Emit(toOtherwise, otherwise);
        this.blocks.Push((otherwise, il.DefineLabel(), false));
    }

    private static MethodInfo Helper(string name) => typeof(Evaluator).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    // [assembly: IgnoresAccessChecksTo(name)]: the runtime honours the attribute by name, so the dynamic assembly
    // defines it for itself.
    private static void AllowAccessTo(AssemblyBuilder assembly, ModuleBuilder module, string name)
    {
        TypeBuilder attribute = module.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        ConstructorBuilder ctor = attribute.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, [typeof(string)]);
        ILGenerator il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes)!);
        il.Emit(OpCodes.Ret);
        Type created = attribute.CreateType();
        assembly.SetCustomAttribute(new CustomAttributeBuilder(created.GetConstructor([typeof(string)])!, [name]));
    }

    private MethodBuilder Method(int nodeId)
    {
        if (!this.methods.TryGetValue(nodeId, out MethodBuilder? method))
        {
            method = this.type.DefineMethod("N" + nodeId, MethodAttributes.Public | MethodAttributes.Static, typeof(bool), NodeParameters);
            method.DefineParameter(1, ParameterAttributes.None, "state");
            method.DefineParameter(2, ParameterAttributes.None, "doc");
            method.DefineParameter(3, ParameterAttributes.None, "index");
            this.methods[nodeId] = method;
        }

        return method;
    }

    private FieldBuilder Constant<T>(T constant)
        where T : notnull
    {
        FieldBuilder field = this.type.DefineField("C" + this.constants.Count, typeof(T), FieldAttributes.Public | FieldAttributes.Static);
        this.constants.Add((field, constant));
        return field;
    }

    // Pushes whether the current token's bit is set in a set of token types.
    private void TokenBit(ushort tokens)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldc_I4, (int)tokens);
        il.Emit(OpCodes.Ldloc, this.token!);
        il.Emit(OpCodes.Shr);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.And);
    }

    // Fails unless the current token is in a set of token types (and, for an integer type, the number is an integer):
    // the token of the method's own value, or of the current property's.
    private void TokenTest(ushort tokens, bool integerOnly, bool lexical, bool atMethodValue)
    {
        ILGenerator il = this.Il;
        if (tokens == 0)
        {
            il.Emit(OpCodes.Br, this.fail);
            return;
        }

        if ((tokens & (tokens - 1)) == 0)
        {
            il.Emit(OpCodes.Ldloc, this.token!);
            il.Emit(OpCodes.Ldc_I4, System.Numerics.BitOperations.TrailingZeroCount((uint)tokens));
            il.Emit(OpCodes.Bne_Un, this.fail);
        }
        else
        {
            this.TokenBit(tokens);
            il.Emit(OpCodes.Brfalse, this.fail);
        }

        if (integerOnly && (tokens & (1 << (int)JsonTokenType.Number)) != 0)
        {
            Label notNumber = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, this.token!);
            il.Emit(OpCodes.Ldc_I4, (int)JsonTokenType.Number);
            il.Emit(OpCodes.Bne_Un, notNumber);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            if (atMethodValue)
            {
                il.Emit(OpCodes.Ldarg_2);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, this.value!);
            }

            il.Emit(lexical ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Call, GenIsInteger);
            il.Emit(OpCodes.Brfalse, this.fail);
            il.MarkLabel(notNumber);
        }
    }

    // Fails unless a helper accepts the current property's value against a constant.
    private void ValueTest(MethodInfo helper, FieldBuilder constant, bool byAddress)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldloc, this.token!);
        il.Emit(byAddress ? OpCodes.Ldsflda : OpCodes.Ldsfld, constant);
        il.Emit(OpCodes.Call, helper);
        il.Emit(OpCodes.Brfalse, this.fail);
    }
}
#endif