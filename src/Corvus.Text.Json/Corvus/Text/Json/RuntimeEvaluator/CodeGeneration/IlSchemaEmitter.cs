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

    // The longest name compared by words; a longer one takes the interpreter's lookup.
    private const int MaxWordsName = 128;

    // The largest set of strings a value is tested against by words in place; a larger one is a hashed lookup.
    private const int MaxWordsSet = 32;

    // The size of a method's IL beyond which a set of strings is no longer tested by words in place, but by a hashed
    // lookup: the in-place test is about 100 bytes of IL a string, and a large object with many enums grows past what
    // the JIT optimises well (clang-format's root object reaches 31,000 bytes with every set in place, and runs 40%
    // slower than with the lookup).
    private const int MaxMethodSizeForWordsSets = 16_000;

    // The largest method (bytes of IL) the JIT is asked to inline, and the largest a caller may be with its callees
    // inlined. Asking for every small method to be inlined gained more on some corpora (importmap 0.83) and lost
    // badly on others (cspell 1.27, fabric-mod 1.18): the JIT optimises a method that has grown too large poorly.
    private const int MaxInlinedMethodSize = 300;
    private const int MaxSizeWithInlinedMethods = 1500;

    // The size of each method's IL, and the calls from one generated method to another (caller, callee), for the
    // choice of the methods the JIT is asked to inline.
    private readonly Dictionary<int, int> sizes = [];
    private readonly List<(int Caller, int Callee)> calls = [];
    private int currentNode;

    // The most names dispatched by words; more take the interpreter's hashed lookup and a jump table, which keeps the
    // method small enough for the JIT to optimise.
    private const int MaxWordDispatch = 512;

    private static readonly Type[] NodeParameters = [typeof(EvaluationState).MakeByRefType(), typeof(IJsonDocument), typeof(int)];

    private static readonly MethodInfo EvalNodeFast = Helper(nameof(Evaluator.EvalNodeFast));
    private static readonly MethodInfo GenToken = Helper(nameof(Evaluator.GenToken));
    private static readonly MethodInfo GenCount = Helper(nameof(Evaluator.GenCount));
    private static readonly MethodInfo GenEnd = Helper(nameof(Evaluator.GenEnd));
    private static readonly MethodInfo GenTokenAndNext = Helper(nameof(Evaluator.GenTokenAndNext));
    private static readonly MethodInfo GenName = Helper(nameof(Evaluator.GenName));
    private static readonly MethodInfo GenIsAscii = Helper(nameof(Evaluator.GenIsAscii));
    private static readonly MethodInfo GenPropertyName = Helper(nameof(Evaluator.GenPropertyName));
    private static readonly MethodInfo GenStringLocation = Helper(nameof(Evaluator.GenStringLocation));
    private static readonly MethodInfo GenWord = Helper(nameof(Evaluator.GenWord));
    private static readonly MethodInfo GenSlowName = Helper(nameof(Evaluator.GenSlowName));
    private static readonly MethodInfo GenEvalGeneral = Helper(nameof(Evaluator.GenEvalGeneral));
    private static readonly MethodInfo GenNameMatches = Helper(nameof(Evaluator.GenNameMatches));
    private static readonly MethodInfo GenFusedValueTests = Helper(nameof(Evaluator.GenFusedValueTests));
    private static readonly MethodInfo GenSelectBranches = Helper(nameof(Evaluator.GenSelectBranches));
    private static readonly MethodInfo GenNameEquals = Helper(nameof(Evaluator.GenNameEquals));
    private static readonly MethodInfo GenSelectByValue = Helper(nameof(Evaluator.GenSelectByValue));
    private static readonly MethodInfo GenOwnConst = Helper(nameof(Evaluator.GenOwnConst));
    private static readonly MethodInfo GenOwnEnum = Helper(nameof(Evaluator.GenOwnEnum));
    private static readonly MethodInfo GenOwnNumber = Helper(nameof(Evaluator.GenOwnNumber));
    private static readonly MethodInfo GenOwnString = Helper(nameof(Evaluator.GenOwnString));
    private static readonly MethodInfo GenTryLong = Helper(nameof(Evaluator.GenTryLong));
    private static readonly MethodInfo GenUniqueItems = Helper(nameof(Evaluator.GenUniqueItems));
    private static readonly MethodInfo GenStringBytes = Helper(nameof(Evaluator.GenStringBytes));
    private static readonly MethodInfo GenStringLengthCounted = Helper(nameof(Evaluator.GenStringLengthCounted));
    private static readonly MethodInfo GenIsInteger = Helper(nameof(Evaluator.GenIsInteger));
    private static readonly MethodInfo GenStringSet = Helper(nameof(Evaluator.GenStringSet));
    private static readonly MethodInfo GenStringConst = Helper(nameof(Evaluator.GenStringConst));

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
    private LocalBuilder? selectedBranches;
    private LocalBuilder? longValue;

    // Scratch locals of the current method, declared once and reused: a word dispatch's location, length and first
    // word, the word a trie compares, and an int. None is live across another use (a dispatch has branched to a case
    // before the case's code runs, and a trie's deeper levels are only reached on a match, from which control never
    // returns to the level above). A fresh local for every use gives a large object hundreds of locals, more than
    // the JIT keeps in registers.
    private LocalBuilder? contained;
    private Label containsSettled;
    private LocalBuilder? dispatchLocation;
    private LocalBuilder? dispatchLength;
    private LocalBuilder? dispatchFirst;
    private LocalBuilder? scratchWord;
    private LocalBuilder? scratchInt;
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
        this.currentNode = nodeId;
        this.il = this.Method(nodeId).GetILGenerator();
        this.fail = this.il.DefineLabel();
        this.token = null;
        this.matched = null;
        this.count = null;
        this.selfToken = null;
        this.ownKeywords = false;
        this.selectedBranches = null;
        this.longValue = null;
        this.contained = null;
        this.dispatchLocation = null;
        this.dispatchLength = null;
        this.dispatchFirst = null;
        this.scratchWord = null;
        this.scratchInt = null;
        this.value = null;
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
        this.sizes[this.currentNode] = il.ILOffset;
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
    public void BeginIfDiscriminated(Discriminator discriminator)
    {
        ILGenerator il = this.Il;
        this.selectedBranches ??= il.DeclareLocal(typeof(ulong));

        // The known string values, without their tag: the values decided by words.
        var strings = new List<byte[]>();
        var masks = new List<ulong>();
        byte[][] tagged = discriminator.KnownValues.Keys;
        ReadOnlySpan<int[]> branches = discriminator.KnownValues.Values;
        for (int i = 0; i < tagged.Length; i++)
        {
            if (tagged[i].Length > 0 && tagged[i][0] == Discriminator.StringTag)
            {
                strings.Add(tagged[i][1..]);
                masks.Add(Mask(branches[i]));
            }
        }

        bool byWords = strings.Count > 0 && strings.Count <= MaxWordsSet && !strings.Exists(k => k.Length > MaxWordsName) && discriminator.PropertyName.Length <= MaxWordsName;
        if (!byWords)
        {
            // The interpreter's selection: finds the property and looks its value up.
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldsfld, this.Constant(discriminator));
            il.Emit(OpCodes.Ldloca, this.selectedBranches);
            il.Emit(OpCodes.Call, GenSelectBranches);
            this.BeginBlock(OpCodes.Brfalse);
            return;
        }

        // In place (Evaluator.TrySelectBranches): an object's first property of the discriminator's name, found by
        // comparing each name's words; its string value against the known values by words. Without the property the
        // discriminator selects nothing in particular, unless every branch requires it (then no branch can match).
        Label otherwise = il.DefineLabel();
        Label discriminated = il.DefineLabel();
        Label found = il.DefineLabel();
        Label nextProperty = il.DefineLabel();
        Label search = il.DefineLabel();
        Label absent = il.DefineLabel();
        Label slowName = il.DefineLabel();
        Label byValue = il.DefineLabel();
        Label unknown = il.DefineLabel();
        LocalBuilder searchEnd = il.DeclareLocal(typeof(int));
        LocalBuilder property = il.DeclareLocal(typeof(int));
        LocalBuilder following = il.DeclareLocal(typeof(int));
        LocalBuilder valueToken = il.DeclareLocal(typeof(int));

        // The word dispatch reads the current value's row: the property being looked at, for this search.
        LocalBuilder? outerValue = this.value;
        this.value = property;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Ldc_I4, (int)JsonTokenType.StartObject);
        il.Emit(OpCodes.Bne_Un, otherwise);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenEnd);
        il.Emit(OpCodes.Stloc, searchEnd);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4, 2 * RowSize);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, property);
        il.MarkLabel(search);
        il.Emit(OpCodes.Ldloc, property);
        il.Emit(OpCodes.Ldc_I4, RowSize);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Ldloc, searchEnd);
        il.Emit(OpCodes.Bge, absent);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, property);
        il.Emit(OpCodes.Ldloca, following);
        il.Emit(OpCodes.Call, GenTokenAndNext);
        il.Emit(OpCodes.Stloc, valueToken);
        this.DispatchByWords([discriminator.PropertyName], GenName, [found], nextProperty, slowName);
        il.MarkLabel(slowName);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, property);
        il.Emit(OpCodes.Ldsfld, this.Constant(discriminator.PropertyName));
        il.Emit(OpCodes.Call, GenNameEquals);
        il.Emit(OpCodes.Brtrue, found);
        il.MarkLabel(nextProperty);
        il.Emit(OpCodes.Ldloc, following);
        il.Emit(OpCodes.Ldc_I4, RowSize);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, property);
        il.Emit(OpCodes.Br, search);

        il.MarkLabel(absent);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(OpCodes.Stloc, this.selectedBranches);
        il.Emit(OpCodes.Br, discriminator.AllRequire ? discriminated : otherwise);

        // The value: a string by words to the mask of its branches, anything else by the interpreter's lookup.
        il.MarkLabel(found);
        il.Emit(OpCodes.Ldloc, valueToken);
        il.Emit(OpCodes.Ldc_I4, (int)JsonTokenType.String);
        il.Emit(OpCodes.Bne_Un, byValue);
        var cases = new Label[strings.Count];
        for (int i = 0; i < cases.Length; i++)
        {
            cases[i] = il.DefineLabel();
        }

        this.DispatchByWords([.. strings], GenStringLocation, cases, unknown, byValue);
        for (int i = 0; i < cases.Length; i++)
        {
            il.MarkLabel(cases[i]);
            il.Emit(OpCodes.Ldc_I8, unchecked((long)masks[i]));
            il.Emit(OpCodes.Stloc, this.selectedBranches);
            il.Emit(OpCodes.Br, discriminated);
        }

        il.MarkLabel(unknown);
        il.Emit(OpCodes.Ldc_I8, unchecked((long)Mask(discriminator.UnknownString)));
        il.Emit(OpCodes.Stloc, this.selectedBranches);
        il.Emit(OpCodes.Br, discriminated);

        il.MarkLabel(byValue);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, property);
        il.Emit(OpCodes.Ldsfld, this.Constant(discriminator));
        il.Emit(OpCodes.Call, GenSelectByValue);
        il.Emit(OpCodes.Stloc, this.selectedBranches);

        il.MarkLabel(discriminated);
        this.value = outerValue;
        this.blocks.Push((otherwise, il.DefineLabel(), false));

        static ulong Mask(int[] selected)
        {
            ulong mask = 0;
            foreach (int branch in selected)
            {
                mask |= 1UL << branch;
            }

            return mask;
        }
    }

    /// <inheritdoc/>
    public void BeginIfBranchSelected(int branch)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.selectedBranches!);
        il.Emit(OpCodes.Ldc_I8, 1L << branch);
        il.Emit(OpCodes.And);
        this.BeginBlock(OpCodes.Brfalse);
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
        LocalBuilder survivors = this.scratchWord ??= il.DeclareLocal(typeof(ulong));
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
        LocalBuilder properties = this.scratchInt ??= il.DeclareLocal(typeof(int));
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
    public void UseSelfAsValue()
    {
        ILGenerator il = this.Il;
        this.value ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stloc, this.value);
        this.LoadSelfToken();
    }

    /// <inheritdoc/>
    public void BeginIfValueToken(ushort tokens)
    {
        if ((tokens & (tokens - 1)) == 0)
        {
            this.Il.Emit(OpCodes.Ldloc, this.token!);
            this.Il.Emit(OpCodes.Ldc_I4, System.Numerics.BitOperations.TrailingZeroCount((uint)tokens));
            this.BeginBlock(OpCodes.Bne_Un);
        }
        else
        {
            this.TokenBit(tokens);
            this.BeginBlock(OpCodes.Brfalse);
        }
    }

    /// <inheritdoc/>
    public void FailUnlessConst(int nodeId)
    {
        this.OwnKeyword(GenOwnConst, nodeId, withToken: true);
    }

    /// <inheritdoc/>
    public void FailUnlessEnum(int nodeId)
    {
        this.OwnKeyword(GenOwnEnum, nodeId, withToken: true);
    }

    /// <inheritdoc/>
    public void FailUnlessNumberKeywords(int nodeId)
    {
        this.OwnKeyword(GenOwnNumber, nodeId, withToken: false);
    }

    /// <inheritdoc/>
    public void FailUnlessStringKeywords(int nodeId)
    {
        this.OwnKeyword(GenOwnString, nodeId, withToken: false);
    }

    /// <inheritdoc/>
    public void BeginIfValueLong()
    {
        ILGenerator il = this.Il;
        this.longValue ??= il.DeclareLocal(typeof(long));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldloca, this.longValue);
        il.Emit(OpCodes.Call, GenTryLong);
        this.BeginBlock(OpCodes.Brfalse);
    }

    /// <inheritdoc/>
    public void FailIfLongBelow(long bound, bool exclusive)
    {
        this.Il.Emit(OpCodes.Ldloc, this.longValue!);
        this.Il.Emit(OpCodes.Ldc_I8, bound);
        this.Il.Emit(exclusive ? OpCodes.Ble : OpCodes.Blt, this.fail);
    }

    /// <inheritdoc/>
    public void FailIfLongAbove(long bound, bool exclusive)
    {
        this.Il.Emit(OpCodes.Ldloc, this.longValue!);
        this.Il.Emit(OpCodes.Ldc_I8, bound);
        this.Il.Emit(exclusive ? OpCodes.Bge : OpCodes.Bgt, this.fail);
    }

    /// <inheritdoc/>
    public void FailUnlessLongMultipleOf(long divisor)
    {
        this.Il.Emit(OpCodes.Ldloc, this.longValue!);
        this.Il.Emit(OpCodes.Ldc_I8, divisor);
        this.Il.Emit(OpCodes.Rem);
        this.Il.Emit(OpCodes.Brtrue, this.fail);
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
    public void FailUnlessName(ChildRef names)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldsfld, this.Constant(names));
        il.Emit(OpCodes.Call, GenPropertyName);
        il.Emit(OpCodes.Brfalse, this.fail);
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
    public void BeginArray(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems, bool uniqueItems = false)
    {
        this.Prologue(JsonTokenType.StartArray, otherTokens, integerOnly, lexical, acceptsArray, minItems, maxItems);
        this.UniqueItems(uniqueItems);
        this.BeginLoop(isObject: false);
    }

    /// <inheritdoc/>
    public void BeginPositionDispatch(int positions)
    {
        ILGenerator il = this.Il;
        this.casesThenCommon = false;
        this.afterCase = this.nextValue;
        this.cases = new Label[positions];
        for (int i = 0; i < positions; i++)
        {
            this.cases[i] = il.DefineLabel();
        }

        this.otherNames = il.DefineLabel();

        // The position counts the items passed (a local, zero when the method is entered).
        LocalBuilder position = il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldloc, position);
        il.Emit(OpCodes.Ldloc, position);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, position);
        if (positions > 0)
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
    public void EndArray()
    {
        this.EndLoop(isObject: false);
        this.Succeed();
    }

    /// <inheritdoc/>
    public void BeginContains(int minContains, int maxContains)
    {
        // The count starts at zero (the method's locals are initialised).
        ILGenerator il = this.Il;
        this.contained ??= il.DeclareLocal(typeof(int));
        this.containsSettled = il.DefineLabel();
        if (maxContains < 0)
        {
            il.Emit(OpCodes.Ldloc, this.contained);
            il.Emit(OpCodes.Ldc_I4, minContains);
            il.Emit(OpCodes.Bge, this.containsSettled);
        }

        this.BeginTry();
    }

    /// <inheritdoc/>
    public void EndContains()
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.contained!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, this.contained!);
        this.OnFail();
        this.EndTry();
        il.MarkLabel(this.containsSettled);
    }

    /// <inheritdoc/>
    public void EndArrayContaining(int minContains, int maxContains)
    {
        this.EndLoop(isObject: false);
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldloc, this.contained!);
        il.Emit(OpCodes.Ldc_I4, minContains);
        il.Emit(OpCodes.Blt, this.fail);
        if (maxContains >= 0)
        {
            il.Emit(OpCodes.Ldloc, this.contained!);
            il.Emit(OpCodes.Ldc_I4, maxContains);
            il.Emit(OpCodes.Bgt, this.fail);
        }

        this.Succeed();
    }

    /// <inheritdoc/>
    public void ReturnArrayWithoutItems(ushort otherTokens, bool integerOnly, bool lexical, bool acceptsArray, int minItems, int maxItems, bool uniqueItems = false)
    {
        this.Prologue(JsonTokenType.StartArray, otherTokens, integerOnly, lexical, acceptsArray, minItems, maxItems);
        this.UniqueItems(uniqueItems);
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

        // Each child's token types tested in turn, not an IL switch: a jump table is one indirect jump for every
        // value, which the processor predicts poorly when the values' types vary (an array of strings and arrays
        // ran a third faster with the comparisons).
        this.selfToken ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenToken);
        il.Emit(OpCodes.Stloc, this.selfToken);
        foreach (KeyValuePair<int, Label> child in byChild)
        {
            int mask = 0;
            for (int i = 0; i < childByToken.Length; i++)
            {
                if (childByToken[i] == child.Key)
                {
                    mask |= 1 << i;
                }
            }

            if ((mask & (mask - 1)) == 0)
            {
                il.Emit(OpCodes.Ldloc, this.selfToken);
                il.Emit(OpCodes.Ldc_I4, System.Numerics.BitOperations.TrailingZeroCount(mask));
                il.Emit(OpCodes.Beq, child.Value);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, mask);
                il.Emit(OpCodes.Ldloc, this.selfToken);
                il.Emit(OpCodes.Ldc_I4, 31);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Shr_Un);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.And);
                il.Emit(OpCodes.Brtrue, child.Value);
            }
        }

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
                    il.Emit(OpCodes.Call, this.Callee(childByToken[i]));
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
        this.DispatchByWords(keys, GenName, this.cases, this.otherNames, slow);

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
        byte[][] keys = allowed.Keys;
        if (keys.Length == 0 || keys.Length > MaxWordsSet || this.Il.ILOffset > MaxMethodSizeForWordsSets || Array.Exists(keys, k => k.Length > MaxWordsName))
        {
            this.ValueTest(GenStringSet, this.Constant(allowed), byAddress: false);
            return;
        }

        // A small set: the value's text against the strings by length and words, as a property's name is
        // dispatched; the lookup only for a value the words cannot decide.
        ILGenerator il = this.Il;
        Label member = il.DefineLabel();
        Label slow = il.DefineLabel();
        var cases = new Label[keys.Length];
        Array.Fill(cases, member);
        il.Emit(OpCodes.Ldloc, this.token!);
        il.Emit(OpCodes.Ldc_I4, (int)JsonTokenType.String);
        il.Emit(OpCodes.Bne_Un, this.fail);
        this.DispatchByWords(keys, GenStringLocation, cases, this.fail, slow);
        il.MarkLabel(slow);
        this.ValueTest(GenStringSet, this.Constant(allowed), byAddress: false);
        il.MarkLabel(member);
    }

    /// <inheritdoc/>
    public void FailUnlessStringConst(byte[] expected)
    {
        this.ValueTest(GenStringConst, this.Constant(expected), byAddress: false);
    }

    /// <inheritdoc/>
    public void FailUnlessLength(in StrictEntry entry)
    {
        // The leaf's type, then a string's length (Evaluator.LengthLeafMatches).
        if (entry.LengthTokenBits != 0)
        {
            this.TokenTest(entry.LengthTokenBits, entry.LengthIntegerOnly, entry.Lexical, atMethodValue: false);
        }

        this.BeginIfValueToken(1 << (int)JsonTokenType.String);
        this.FailUnlessStringLength(entry.MinLength, entry.MaxLength);
        this.EndIf();
    }

    /// <inheritdoc/>
    public void FailUnlessStringLength(int minLength, int maxLength)
    {
        // An unescaped value of at most maxLength bytes, and of enough bytes that even four to a rune reach
        // minLength, is within the bounds; any other value is counted.
        ILGenerator il = this.Il;
        Label counted = il.DefineLabel();
        Label within = il.DefineLabel();
        LocalBuilder bytes = this.scratchInt ??= il.DeclareLocal(typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Call, GenStringBytes);
        il.Emit(OpCodes.Stloc, bytes);
        il.Emit(OpCodes.Ldloc, bytes);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Blt, counted);

        // Text the parser found all ASCII (most text) has one character to a byte: the byte length is the length.
        Label notAscii = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Call, GenIsAscii);
        il.Emit(OpCodes.Brfalse, notAscii);
        if (maxLength >= 0)
        {
            il.Emit(OpCodes.Ldloc, bytes);
            il.Emit(OpCodes.Ldc_I4, maxLength);
            il.Emit(OpCodes.Bgt, this.fail);
        }

        if (minLength >= 0)
        {
            il.Emit(OpCodes.Ldloc, bytes);
            il.Emit(OpCodes.Ldc_I4, minLength);
            il.Emit(OpCodes.Blt, this.fail);
        }

        il.Emit(OpCodes.Br, within);
        il.MarkLabel(notAscii);
        if (maxLength >= 0)
        {
            il.Emit(OpCodes.Ldloc, bytes);
            il.Emit(OpCodes.Ldc_I4, maxLength);
            il.Emit(OpCodes.Bgt, counted);
        }

        if (minLength >= 0)
        {
            il.Emit(OpCodes.Ldloc, bytes);
            il.Emit(OpCodes.Ldc_I4_3);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Shr);
            il.Emit(OpCodes.Ldc_I4, minLength);
            il.Emit(OpCodes.Blt, counted);
        }

        il.Emit(OpCodes.Br, within);
        il.MarkLabel(counted);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldc_I4, minLength);
        il.Emit(OpCodes.Ldc_I4, maxLength);
        il.Emit(OpCodes.Call, GenStringLengthCounted);
        il.Emit(OpCodes.Brfalse, this.fail);
        il.MarkLabel(within);
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
            il.Emit(OpCodes.Call, this.Callee(child));
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
        this.ChooseInlinedMethods();
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

    // Fails unless one of the interpreter's keyword evaluations accepts the current value for a node.
    private void OwnKeyword(MethodInfo helper, int nodeId, bool withToken)
    {
        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldc_I4, nodeId);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, this.value!);
        if (withToken)
        {
            il.Emit(OpCodes.Ldloc, this.token!);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, helper);
        il.Emit(OpCodes.Brfalse, this.fail);
    }

    // Fails when the method's value (an array) has two equal items.
    private void UniqueItems(bool uniqueItems)
    {
        if (!uniqueItems)
        {
            return;
        }

        ILGenerator il = this.Il;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, GenUniqueItems);
        il.Emit(OpCodes.Brfalse, this.fail);
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
            il.Emit(OpCodes.Call, this.Callee(child));
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

    // The dispatch on the text at a location (a property's name, or a string value): by length, then by a trie of
    // words within each length. Control goes to the case of the key the text equals, to none when it equals no key,
    // and to slow for text the words cannot decide (escaped, at the very end of the document, or too long).
    // Branches to the target of the case whose key a local holds, or to none: a tree of comparisons over the keys in
    // ascending order. An IL switch compiles to a jump table, which is one indirect jump for every value dispatched;
    // the processor predicts the tree's conditional branches from the sequence of values far better (a tree for the
    // length of each property's name made plain objects 8 to 18% faster than the table).
    private void BranchByValue(LocalBuilder value, (int Key, Label Target)[] sorted, int from, int to, Label none)
    {
        ILGenerator il = this.Il;
        if (to - from <= 2)
        {
            for (int i = from; i < to; i++)
            {
                il.Emit(OpCodes.Ldloc, value);
                il.Emit(OpCodes.Ldc_I4, sorted[i].Key);
                il.Emit(OpCodes.Beq, sorted[i].Target);
            }

            il.Emit(OpCodes.Br, none);
            return;
        }

        int middle = (from + to) / 2;
        Label upper = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, value);
        il.Emit(OpCodes.Ldc_I4, sorted[middle].Key);
        il.Emit(OpCodes.Bge, upper);
        this.BranchByValue(value, sorted, from, middle, none);
        il.MarkLabel(upper);
        this.BranchByValue(value, sorted, middle, to, none);
    }

    private void DispatchByWords(byte[][] keys, MethodInfo locate, Label[] cases, Label none, Label slow)
    {
        ILGenerator il = this.Il;
        LocalBuilder location = this.dispatchLocation ??= il.DeclareLocal(typeof(int));
        LocalBuilder length = this.dispatchLength ??= il.DeclareLocal(typeof(int));
        LocalBuilder first = this.dispatchFirst ??= il.DeclareLocal(typeof(ulong));

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

        if (keys.Length > MaxWordDispatch)
        {
            byLength.Clear();
            il.Emit(OpCodes.Br, slow);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, this.value!);
        il.Emit(OpCodes.Ldloca, length);
        il.Emit(OpCodes.Call, locate);
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
            // A tree of comparisons on the length, not a jump table: see BranchByValue.
            var byLengthLabels = new (int Key, Label Target)[lengthLabels.Count];
            int at = 0;
            foreach (int nameLength in byLength.Keys)
            {
                byLengthLabels[at++] = (nameLength, lengthLabels[nameLength]);
            }

            this.BranchByValue(length, byLengthLabels, 0, byLengthLabels.Length, none);
        }

        il.Emit(OpCodes.Br, none);

        // Within a length, a trie of words: at each position the name's word is read once and compared with the
        // distinct words the remaining names have there, so names that share a prefix share its comparisons.
        foreach (KeyValuePair<int, List<int>> sameLength in byLength)
        {
            int nameLength = sameLength.Key;
            if (nameLength > MaxWordsName)
            {
                continue;
            }

            il.MarkLabel(lengthLabels[nameLength]);
            if (nameLength == 0)
            {
                il.Emit(OpCodes.Br, cases[sameLength.Value[0]]);
                continue;
            }

            // The word positions: 0, 8, ... and the last (overlapping) one, for names longer than 8 bytes.
            var positions = new List<int>();
            if (nameLength <= sizeof(ulong))
            {
                positions.Add(0);
            }
            else
            {
                for (int position = 0; position + sizeof(ulong) < nameLength; position += sizeof(ulong))
                {
                    positions.Add(position);
                }

                positions.Add(nameLength - sizeof(ulong));
            }

            this.WordTrie(keys, sameLength.Value, positions, 0, Math.Min(nameLength, sizeof(ulong)), location, first, cases, none);
        }
    }

    // The word of a name at a position, as the document's text holds it: a name shorter than a word padded with
    // zeros (the generated code masks the word it reads to the name's bytes).
    private static ulong WordOf(byte[] name, int position, int width)
    {
        Span<byte> padded = stackalloc byte[sizeof(ulong)];
        padded.Clear();
        name.AsSpan(position, width).CopyTo(padded);
        return MemoryMarshal.Read<ulong>(padded);
    }

    private void WordTrie(byte[][] keys, List<int> candidates, List<int> positions, int depth, int firstWidth, LocalBuilder location, LocalBuilder first, Label[] cases, Label none)
    {
        ILGenerator il = this.Il;
        if (depth == positions.Count)
        {
            // Names are distinct, so one candidate remains.
            il.Emit(OpCodes.Br, cases[candidates[0]]);
            return;
        }

        int position = positions[depth];
        int width = depth == 0 ? firstWidth : sizeof(ulong);
        var byWord = new Dictionary<ulong, List<int>>();
        var words = new List<ulong>();
        foreach (int index in candidates)
        {
            ulong word = WordOf(keys[index], position, width);
            if (!byWord.TryGetValue(word, out List<int>? sameWord))
            {
                byWord[word] = sameWord = [];
                words.Add(word);
            }

            sameWord.Add(index);
        }

        // The name's word here: the first is already read (masked to the name's bytes when it is shorter).
        LocalBuilder current = this.scratchWord ??= il.DeclareLocal(typeof(ulong));
        if (depth == 0)
        {
            il.Emit(OpCodes.Ldloc, first);
            if (width < sizeof(ulong))
            {
                il.Emit(OpCodes.Ldc_I8, unchecked((long)WordOf([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], 0, width)));
                il.Emit(OpCodes.And);
            }
        }
        else
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, location);
            il.Emit(OpCodes.Ldc_I4, position);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Call, GenWord);
        }

        il.Emit(OpCodes.Stloc, current);
        foreach (ulong word in words)
        {
            Label other = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, current);
            il.Emit(OpCodes.Ldc_I8, unchecked((long)word));
            il.Emit(OpCodes.Bne_Un, other);
            this.WordTrie(keys, byWord[word], positions, depth + 1, firstWidth, location, first, cases, none);
            il.MarkLabel(other);
        }

        il.Emit(OpCodes.Br, none);
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

    private MethodBuilder Callee(int nodeId)
    {
        this.calls.Add((this.currentNode, nodeId));
        return this.Method(nodeId);
    }

    // Asks the JIT to inline the small methods (a map of strings, an array of strings, a small closed object, a
    // choice between a string and an array), where every method that calls them stays small with them inlined. A
    // method's size counts the methods inlined into it, and a method that reaches itself is never inlined.
    private void ChooseInlinedMethods()
    {
        var callees = new Dictionary<int, List<int>>();
        foreach ((int caller, int callee) in this.calls)
        {
            if (!callees.TryGetValue(caller, out List<int>? list))
            {
                callees[caller] = list = [];
            }

            list.Add(callee);
        }

        var inlined = new HashSet<int>();
        foreach (KeyValuePair<int, int> size in this.sizes)
        {
            if (size.Value <= MaxInlinedMethodSize && !Reaches(size.Key, size.Key, []))
            {
                inlined.Add(size.Key);
            }
        }

        // Take candidates away until every one is small with its own callees inlined, and has no caller that
        // grows too large.
        while (true)
        {
            var effective = new Dictionary<int, int>();
            var dropped = new List<int>();
            foreach (int candidate in inlined)
            {
                if (Effective(candidate) > MaxInlinedMethodSize)
                {
                    dropped.Add(candidate);
                }
            }

            foreach ((int caller, int callee) in this.calls)
            {
                if (inlined.Contains(callee) && Effective(caller) > MaxSizeWithInlinedMethods)
                {
                    dropped.Add(callee);
                }
            }

            if (dropped.Count == 0)
            {
                break;
            }

            inlined.ExceptWith(dropped);

            int Effective(int nodeId)
            {
                if (!effective.TryGetValue(nodeId, out int size))
                {
                    size = this.sizes.GetValueOrDefault(nodeId);
                    effective[nodeId] = size;
                    foreach (int callee in callees.GetValueOrDefault(nodeId) ?? [])
                    {
                        if (inlined.Contains(callee))
                        {
                            size += Effective(callee);
                        }
                    }

                    effective[nodeId] = size;
                }

                return size;
            }
        }

        foreach (int nodeId in inlined)
        {
            this.methods[nodeId].SetImplementationFlags(MethodImplAttributes.IL | MethodImplAttributes.AggressiveInlining);
        }

        bool Reaches(int from, int target, HashSet<int> visited)
        {
            foreach (int callee in callees.GetValueOrDefault(from) ?? [])
            {
                if (callee == target || (visited.Add(callee) && Reaches(callee, target, visited)))
                {
                    return true;
                }
            }

            return false;
        }
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