// <copyright file="FusedObjects.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
/// in-place applicators (<c>allOf</c>, <c>$ref</c>, <c>if</c>/<c>then</c>/<c>else</c>) and finished by
/// <c>unevaluatedProperties</c>.
/// </summary>
/// <remarks>
/// <para>
/// The general path evaluates each applicator branch as a separate pass over the instance, each with its own
/// property lookups and a scratch set of evaluated bits merged into the parent's, and then walks the instance
/// once more for <c>unevaluatedProperties</c>. The fused plan resolves every property name known to any branch at
/// compile time to the list of child schemas that apply to it (a branch's own property schema, its matching
/// pattern-property schemas, or its <c>additionalProperties</c> when neither matched), so evaluation is one lookup
/// per instance property, and tracks which properties some branch covered so the unevaluated check needs no second
/// analysis.
/// </para>
/// <para>
/// Branches under <c>then</c> or <c>else</c> apply only when the <c>if</c> holds; the plan supports <c>if</c>
/// schemas that are pure <c>required</c> lists, whose truth is known once the properties have been seen, and
/// defers those branches' applications to a second step over the properties they touched.
/// </para>
/// <para>
/// Flag mode only: collecting mode keeps the general path, which produces the per-branch results and annotations.
/// </para>
/// </remarks>
internal sealed class FusedObject
{
    /// <summary>Every property name any branch knows (its <c>properties</c> or <c>required</c>), with what applies to it.</summary>
    public Utf8NameMap<FusedEntry> Entries = null!;

    /// <summary>The entries in index order.</summary>
    public FusedEntry[] EntryList = [];

    /// <summary>The branches, the node itself first.</summary>
    public FusedContributor[] Contributors = [];

    /// <summary>The conditions (an <c>if</c>'s tests, or a dependency's presence), each possibly gated on another.</summary>
    public FusedCondition[] Conditions = [];

    /// <summary>Required-only <c>oneOf</c>/<c>anyOf</c> keywords, decided from the seen bits after the pass.</summary>
    public FusedAlternative[] Alternatives = [];

    /// <summary>
    /// The <c>anyOf</c>/<c>oneOf</c> groups whose branches carry object keywords: each branch is a contributor whose
    /// failure marks the branch rather than the object, and the group is decided from the surviving branches.
    /// </summary>
    public FusedAltGroup[] AltGroups = [];

    /// <summary>Whether any branch has pattern properties or additional properties, so unknown names need resolving.</summary>
    public bool ResolvesUnknownNames;

    /// <summary>Whether any branch is conditional.</summary>
    public bool HasConditions;

    /// <summary>Whether any branch bounds the property count.</summary>
    public bool HasCountBounds;

    /// <summary>The <c>unevaluatedProperties</c> schema, or none.</summary>
    public ChildRef Unevaluated = ChildRef.None;
}

/// <summary>A known property name and the child schemas that apply to it, per branch.</summary>
internal sealed class FusedEntry
{
    public byte[] Name = [];
    public int Index;
    public FusedApplication[] Applications = [];
    public bool AnyConditional;

    /// <summary>The conditions whose value test this property decides, checked as the property is passed.</summary>
    public FusedValueTest[] ValueTests = [];

    /// <summary>The tests with a key set, in the order of the bits of <see cref="MergedAllowed"/>'s masks (at most 64).</summary>
    public FusedValueTest[] KeyedTests = [];

    /// <summary>Tagged value to the mask of <see cref="KeyedTests"/> that allow it: one lookup decides every keyed test.</summary>
    public Utf8NameMap<FusedTestMask>? MergedAllowed;

    /// <summary>The tests that match a pattern instead of a key set.</summary>
    public FusedValueTest[] PatternTests = [];

    public bool HasValueTests => this.ValueTests.Length > 0;
}

/// <summary>The keyed tests a value satisfies, as a bit per test.</summary>
internal sealed class FusedTestMask(ulong mask)
{
    public readonly ulong Mask = mask;
}

/// <summary>
/// A condition's test on one property's value: the property, when present, must hold one of the allowed constants
/// (keys tagged by kind exactly as <see cref="Discriminator"/> keys are).
/// </summary>
internal sealed class FusedValueTest
{
    public int Condition;
    public int Entry;

    /// <summary>The allowed tagged keys, or null when the test is a <see cref="Pattern"/>.</summary>
    public Utf8NameMap<object>? Allowed;

    /// <summary>A <c>pattern</c> test on string values; a non-string value passes unless <see cref="RequiresString"/>.</summary>
    public PatternMatcher? Pattern;

    public bool RequiresString;
}

/// <summary>
/// A <c>oneOf</c> or <c>anyOf</c> whose branches are nothing but <c>required</c> lists: after the pass, the number
/// of branches whose names were all seen decides it. Gated like a contributor when it sits under a condition.
/// </summary>
/// <summary>An alternative group of contributors: at least one branch must survive, or exactly one for <c>oneOf</c>.</summary>
internal sealed class FusedAltGroup
{
    public bool ExactlyOne;
    public int BranchCount;
}

internal sealed class FusedAlternative
{
    public int Condition = -1;
    public bool Polarity = true;
    public bool ExactlyOne;
    public int[][] Branches = [];
}

/// <summary>One child schema applying to a property on behalf of a branch; a node of -1 covers without evaluation.</summary>
internal readonly struct FusedApplication(int contributor, int node, TypeMask inlineType, bool inlineLexical, Utf8NameMap<object>? inlineEnum = null)
{
    public readonly int Contributor = contributor;
    public readonly int Node = node;

    /// <summary>The child's type mask when it is a type-only leaf (tested in place of a call), else <see cref="TypeMask.None"/>.</summary>
    public readonly TypeMask InlineType = inlineType;

    public readonly bool InlineLexical = inlineLexical;

    /// <summary>The child's string set when it is a string-enum leaf (tested in place of a call).</summary>
    public readonly Utf8NameMap<object>? InlineEnum = inlineEnum;

    /// <summary>The token types <see cref="InlineType"/> accepts, one bit per token type; 0 when there is no type to test.</summary>
    public readonly ushort TokenBits = StrictEntry.TokenBitsOf(inlineType);

    public readonly bool IntegerOnly = (inlineType & TypeMask.Integer) != 0 && (inlineType & TypeMask.Number) == 0;
}

/// <summary>A branch: its condition, what it does with names no entry knows, and what it requires.</summary>
internal sealed class FusedContributor
{
    public int Condition = -1;
    public bool Polarity = true;

    /// <summary>The alternative group this contributor is a branch of, or -1 for an ordinary contributor.</summary>
    public int AltGroup = -1;

    public int AltBranch;
    public PatternPropertyEntry[]? Patterns;
    public int AdditionalNode = -1;
    public bool AdditionalCoversOnly;
    public int[] RequiredBits = [];
    public int MinProperties = -1;
    public int MaxProperties = -1;
}

/// <summary>An <c>if</c> that is a pure <c>required</c> list: it holds when every listed name is present.</summary>
internal sealed class FusedCondition
{
    public int[] RequiredBits = [];

    /// <summary>Value tests on properties (see <see cref="FusedValueTest"/>); a property that is absent passes its test.</summary>
    public FusedValueTest[] ValueTests = [];

    /// <summary>
    /// The enclosing condition and the polarity under which this one is reached (a nested <c>if</c>, or an <c>if</c>
    /// inside a dependent schema), or -1 at the top level. A contributor applies when its own condition matches its
    /// polarity and every condition on the chain above matches too.
    /// </summary>
    public int Gate = -1;

    public bool GatePolarity = true;
}

/// <summary>A condition as collected: an <c>if</c> schema, or the presence of a property for a dependency.</summary>
internal sealed class PendingCondition
{
    public SchemaNode? Test;
    public byte[]? DependencyName;
    public int Gate = -1;
    public bool GatePolarity = true;
}

/// <summary>Builds fused object plans over a compiled node graph.</summary>
internal static class FusedObjects
{
    private const int MaxNames = SchemaNode.InlineBitWords * 64;
    private const int MaxConditions = 64;
    private const int MaxContributors = 64;
    private const int MaxAltGroups = 8;

#if STJ
    private static readonly bool Disabled = false;
#else
    private static readonly bool Disabled = Environment.GetEnvironmentVariable("CORVUS_RT_NO_FUSE") == "1";
#endif

    /// <summary>
    /// Computes the fused plan for every eligible node. Runs after in-place cycles and pure-reference elision are
    /// known and before plans are selected; a program that keeps a dynamic scope is left alone, because fusing
    /// would skip the resource-scope pushes its references depend on.
    /// </summary>
    public static void Compute(SchemaNode[] nodes)
    {
        foreach (SchemaNode node in nodes)
        {
            node.Fused = null;
        }

        if (Disabled)
        {
            return;
        }

        // A fused plan applies its contributors' keywords without entering the contributors as nodes, so the dynamic
        // scope below it would differ from the general path's. That only matters where a live dynamic reference can
        // be reached, so only those nodes keep the general plan; the rest of the program fuses as usual.
        bool[] reachesDynamic = ReachesDynamicReference(nodes);
        foreach (SchemaNode node in nodes)
        {
            if (reachesDynamic[node.Id])
            {
                continue;
            }

            node.Fused = TryFuse(nodes, node);
        }
    }

    /// <summary>Marks every node from which a live dynamic reference is reachable through any child.</summary>
    private static bool[] ReachesDynamicReference(SchemaNode[] nodes)
    {
        bool[] reaches = new bool[nodes.Length];
        var children = new List<int>();
        foreach (SchemaNode node in nodes)
        {
            reaches[node.Id] = node.DynamicRef is { NeedsScope: true };
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (SchemaNode node in nodes)
            {
                if (reaches[node.Id])
                {
                    continue;
                }

                children.Clear();
                node.CollectChildren(children);
                foreach (int child in children)
                {
                    if (reaches[child])
                    {
                        reaches[node.Id] = true;
                        changed = true;
                        break;
                    }
                }
            }
        }

        return reaches;
    }

    private static FusedObject? TryFuse(SchemaNode[] nodes, SchemaNode node)
    {
        if (!IsObjectBranch(node, allowUnevaluated: true) || node.InPlaceCycle)
        {
            return null;
        }

        var contributors = new List<(SchemaNode Node, int Condition, bool Polarity, int AltGroup, int AltBranch)>();
        var conditions = new List<PendingCondition>();
        var extras = new List<(int Condition, bool Polarity, byte[][] RequiredNames)>();
        var alternatives = new List<(int Condition, bool Polarity, bool ExactlyOne, byte[][][] Branches)>();
        var altGroups = new List<(bool ExactlyOne, int BranchCount)>();
        var context = new CollectContext(nodes, contributors, conditions, extras, alternatives, altGroups, !node.UnevaluatedProperties.IsPresent);
        if (!Collect(context, node, -1, true))
        {
            return null;
        }

        if (contributors.Count > MaxContributors || conditions.Count > MaxConditions)
        {
            return null;
        }

        // Fusing pays when a pass over every instance property is unavoidable (unevaluatedProperties) or when it
        // replaces several passes: two or more branches with object keywords, an if whose condition the seen bits
        // decide, or required-only alternatives. A node whose object keywords are all its own keeps its object plan,
        // which already takes dependencies in its one pass.
        bool hasIfCondition = false;
        foreach (PendingCondition condition in conditions)
        {
            hasIfCondition |= condition.Test is not null;
        }

        if (!node.UnevaluatedProperties.IsPresent && !hasIfCondition && alternatives.Count == 0 && altGroups.Count == 0)
        {
            int effective = 0;
            foreach ((SchemaNode branch, _, _, _, _) in contributors)
            {
                if (branch.Properties is not null || branch.PatternProperties is not null || branch.AdditionalProperties.IsPresent
                    || branch.RequiredNames is { Length: > 0 } || branch.MinProperties >= 0 || branch.MaxProperties >= 0)
                {
                    effective++;
                }
            }

            if (effective < 2)
            {
                return null;
            }
        }

        // Every name any branch or condition knows gets a bit.
        var names = new List<byte[]>();
        var nameIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        int NameBit(byte[] name)
        {
            string key = System.Text.Encoding.UTF8.GetString(name);
            if (!nameIndex.TryGetValue(key, out int bit))
            {
                bit = names.Count;
                names.Add(name);
                nameIndex.Add(key, bit);
            }

            return bit;
        }

        foreach ((SchemaNode branch, _, _, _, _) in contributors)
        {
            if (branch.Properties is Utf8NameMap<PropertyEntry> properties)
            {
                foreach (byte[] name in properties.Keys)
                {
                    NameBit(name);
                }
            }

            if (branch.RequiredNames is byte[][] required)
            {
                foreach (byte[] name in required)
                {
                    NameBit(name);
                }
            }
        }

        foreach (PendingCondition condition in conditions)
        {
            foreach (byte[] name in condition.Test?.RequiredNames ?? [])
            {
                NameBit(name);
            }

            if (condition.DependencyName is byte[] dependencyName)
            {
                NameBit(dependencyName);
            }
        }

        foreach ((_, _, byte[][] requiredNames) in extras)
        {
            foreach (byte[] name in requiredNames)
            {
                NameBit(name);
            }
        }

        foreach ((_, _, _, byte[][][] branches) in alternatives)
        {
            foreach (byte[][] branch in branches)
            {
                foreach (byte[] name in branch)
                {
                    NameBit(name);
                }
            }
        }

        if (names.Count > MaxNames)
        {
            return null;
        }

        var fused = new FusedObject
        {
            Unevaluated = node.UnevaluatedProperties,
            HasConditions = conditions.Count > 0,
        };

        var built = new FusedContributor[contributors.Count];
        for (int c = 0; c < contributors.Count; c++)
        {
            (SchemaNode branch, int condition, bool polarity, int altGroup, int altBranch) = contributors[c];
            var contributor = new FusedContributor
            {
                Condition = condition,
                Polarity = polarity,
                AltGroup = altGroup,
                AltBranch = altBranch,
                Patterns = branch.PatternProperties,
                MinProperties = branch.MinProperties,
                MaxProperties = branch.MaxProperties,
            };

            if (branch.AdditionalProperties.IsPresent)
            {
                SchemaNode additional = nodes[branch.AdditionalProperties.FastNode];
                contributor.AdditionalNode = additional.AlwaysTrue ? -1 : branch.AdditionalProperties.FastNode;
                contributor.AdditionalCoversOnly = additional.AlwaysTrue;
            }

            if (branch.RequiredNames is byte[][] required)
            {
                contributor.RequiredBits = new int[required.Length];
                for (int i = 0; i < required.Length; i++)
                {
                    contributor.RequiredBits[i] = NameBit(required[i]);
                }
            }

            fused.ResolvesUnknownNames |= contributor.Patterns is not null || branch.AdditionalProperties.IsPresent;
            fused.HasCountBounds |= contributor.MinProperties >= 0 || contributor.MaxProperties >= 0;
            built[c] = contributor;
        }

        if (extras.Count > 0)
        {
            var all = new FusedContributor[built.Length + extras.Count];
            built.CopyTo(all, 0);
            for (int e = 0; e < extras.Count; e++)
            {
                (int condition, bool polarity, byte[][] requiredNames) = extras[e];
                var bits = new int[requiredNames.Length];
                for (int i = 0; i < bits.Length; i++)
                {
                    bits[i] = NameBit(requiredNames[i]);
                }

                all[built.Length + e] = new FusedContributor { Condition = condition, Polarity = polarity, RequiredBits = bits };
            }

            built = all;
        }

        fused.Contributors = built;

        var builtGroups = new FusedAltGroup[altGroups.Count];
        for (int g = 0; g < altGroups.Count; g++)
        {
            builtGroups[g] = new FusedAltGroup { ExactlyOne = altGroups[g].ExactlyOne, BranchCount = altGroups[g].BranchCount };
        }

        fused.AltGroups = builtGroups;

        var builtAlternatives = new FusedAlternative[alternatives.Count];
        for (int a = 0; a < alternatives.Count; a++)
        {
            (int condition, bool polarity, bool exactlyOne, byte[][][] branches) = alternatives[a];
            var branchBits = new int[branches.Length][];
            for (int b = 0; b < branches.Length; b++)
            {
                branchBits[b] = new int[branches[b].Length];
                for (int i = 0; i < branches[b].Length; i++)
                {
                    branchBits[b][i] = NameBit(branches[b][i]);
                }
            }

            builtAlternatives[a] = new FusedAlternative { Condition = condition, Polarity = polarity, ExactlyOne = exactlyOne, Branches = branchBits };
        }

        fused.Alternatives = builtAlternatives;

        var builtConditions = new FusedCondition[conditions.Count];
        var valueTestsByEntry = new Dictionary<int, List<FusedValueTest>>();
        for (int i = 0; i < conditions.Count; i++)
        {
            PendingCondition pending = conditions[i];
            byte[][] required = pending.Test?.RequiredNames ?? (pending.DependencyName is byte[] dependency ? [dependency] : []);
            var bits = new int[required.Length];
            for (int j = 0; j < required.Length; j++)
            {
                bits[j] = NameBit(required[j]);
            }

            var tests = new List<FusedValueTest>();
            if (pending.Test?.Properties is Utf8NameMap<PropertyEntry> testProperties)
            {
                foreach (PropertyEntry entry in testProperties.Values)
                {
                    if (!entry.Schema.IsPresent)
                    {
                        continue;
                    }

                    // IsSupportedCondition accepted the schema, so it keys or is a pattern.
                    FusedValueTest test = ValueTestFor(nodes[entry.Schema.FastNode])!;
                    test.Condition = i;
                    test.Entry = NameBit(entry.Name);
                    tests.Add(test);
                    if (!valueTestsByEntry.TryGetValue(test.Entry, out List<FusedValueTest>? list))
                    {
                        list = [];
                        valueTestsByEntry.Add(test.Entry, list);
                    }

                    list.Add(test);
                }
            }

            builtConditions[i] = new FusedCondition { RequiredBits = bits, ValueTests = [.. tests], Gate = pending.Gate, GatePolarity = pending.GatePolarity };
        }

        fused.Conditions = builtConditions;

        // Resolve every known name against every branch at compile time.
        var entries = new FusedEntry[names.Count];
        var mapEntries = new List<KeyValuePair<byte[], FusedEntry>>(names.Count);
        var applications = new List<FusedApplication>();
        for (int i = 0; i < names.Count; i++)
        {
            byte[] name = names[i];
            applications.Clear();
            bool anyConditional = false;
            for (int c = 0; c < contributors.Count; c++)
            {
                SchemaNode branch = contributors[c].Node;
                bool conditional = built[c].Condition >= 0;
                bool matched = false;
                if (branch.Properties is Utf8NameMap<PropertyEntry> properties && properties.TryGetValue(name, out PropertyEntry? entry) && entry.Schema.IsPresent)
                {
                    matched = true;
                    applications.Add(Application(nodes, c, entry.Schema));
                    anyConditional |= conditional;
                }

                if (branch.PatternProperties is PatternPropertyEntry[] patterns)
                {
                    foreach (PatternPropertyEntry pattern in patterns)
                    {
                        if (pattern.Matcher.IsMatch(name))
                        {
                            matched = true;
                            applications.Add(Application(nodes, c, pattern.Schema));
                            anyConditional |= conditional;
                        }
                    }
                }

                if (!matched && branch.AdditionalProperties.IsPresent)
                {
                    applications.Add(Application(nodes, c, branch.AdditionalProperties));
                    anyConditional |= conditional;
                }
            }

            var fusedEntry = new FusedEntry { Name = name, Index = i, Applications = [.. applications], AnyConditional = anyConditional };
            if (valueTestsByEntry.TryGetValue(i, out List<FusedValueTest>? entryTests))
            {
                fusedEntry.ValueTests = [.. entryTests];
                MergeValueTests(fusedEntry);
            }

            entries[i] = fusedEntry;
            mapEntries.Add(new KeyValuePair<byte[], FusedEntry>(name, fusedEntry));
        }

        fused.EntryList = entries;
        fused.Entries = new Utf8NameMap<FusedEntry>(mapEntries);
        return fused;
    }

    /// <summary>
    /// Splits an entry's value tests into pattern tests and keyed tests, and merges the keyed tests' sets into one
    /// map from tagged value to the mask of tests that allow it (up to 64 keyed tests; beyond that the keyed tests
    /// are consulted one by one).
    /// </summary>
    private static void MergeValueTests(FusedEntry entry)
    {
        var keyed = new List<FusedValueTest>();
        var patterns = new List<FusedValueTest>();
        foreach (FusedValueTest test in entry.ValueTests)
        {
            (test.Pattern is not null ? patterns : keyed).Add(test);
        }

        entry.PatternTests = [.. patterns];
        entry.KeyedTests = [.. keyed];
        if (keyed.Count == 0 || keyed.Count > 64)
        {
            entry.MergedAllowed = null;
            return;
        }

        var masks = new Dictionary<string, (byte[] Key, ulong Mask)>(StringComparer.Ordinal);
        for (int t = 0; t < keyed.Count; t++)
        {
            foreach (byte[] key in keyed[t].Allowed!.Keys)
            {
                string text = Convert.ToBase64String(key);
                masks[text] = (key, (masks.TryGetValue(text, out (byte[] Key, ulong Mask) existing) ? existing.Mask : 0UL) | (1UL << t));
            }
        }

        var pairs = new List<KeyValuePair<byte[], FusedTestMask>>(masks.Count);
        foreach ((byte[] key, ulong mask) in masks.Values)
        {
            pairs.Add(new KeyValuePair<byte[], FusedTestMask>(key, new FusedTestMask(mask)));
        }

        entry.MergedAllowed = new Utf8NameMap<FusedTestMask>(pairs);
    }

    /// <summary>A child that is <c>true</c> covers the property without a call; a type-only leaf is a token-type test.</summary>
    private static FusedApplication Application(SchemaNode[] nodes, int contributor, in ChildRef child)
    {
        SchemaNode target = nodes[child.FastNode];
        if (target.AlwaysTrue)
        {
            return new FusedApplication(contributor, -1, TypeMask.None, false);
        }

        if (target.IsTypeOnly)
        {
            return new FusedApplication(contributor, child.FastNode, target.Type, target.Dialect == JsonSchemaDialect.Draft4);
        }

        return SchemaCompiler.IsStringEnumOnly(target)
            ? new FusedApplication(contributor, child.FastNode, TypeMask.None, false, target.EnumStrings)
            : new FusedApplication(contributor, child.FastNode, TypeMask.None, false);
    }

    /// <summary>The lists a collection fills.</summary>
    private sealed class CollectContext(
        SchemaNode[] nodes,
        List<(SchemaNode Node, int Condition, bool Polarity, int AltGroup, int AltBranch)> contributors,
        List<PendingCondition> conditions,
        List<(int Condition, bool Polarity, byte[][] RequiredNames)> extras,
        List<(int Condition, bool Polarity, bool ExactlyOne, byte[][][] Branches)> alternatives,
        List<(bool ExactlyOne, int BranchCount)> altGroups,
        bool allowAltGroups)
    {
        public SchemaNode[] Nodes { get; } = nodes;

        public List<(SchemaNode Node, int Condition, bool Polarity, int AltGroup, int AltBranch)> Contributors { get; } = contributors;

        public List<(bool ExactlyOne, int BranchCount)> AltGroups { get; } = altGroups;

        /// <summary>Alternative groups with object keywords are taken only where coverage is not tracked, since a failed branch must not cover.</summary>
        public bool AllowAltGroups { get; } = allowAltGroups;

        public List<PendingCondition> Conditions { get; } = conditions;

        public List<(int Condition, bool Polarity, byte[][] RequiredNames)> Extras { get; } = extras;

        public List<(int Condition, bool Polarity, bool ExactlyOne, byte[][][] Branches)> Alternatives { get; } = alternatives;
    }

    /// <summary>
    /// Walks the in-place applicators of a branch, adding every object branch with its condition. Conditions nest:
    /// an <c>if</c> under a condition, or the dependent schemas of a branch under one, get a condition gated on it.
    /// Fails when a branch cannot be fused.
    /// </summary>
    private static bool Collect(CollectContext ctx, SchemaNode branch, int condition, bool polarity)
    {
        SchemaNode[] nodes = ctx.Nodes;
        if (branch.AlwaysTrue)
        {
            return true;
        }

        if (branch.AlwaysFalse || branch.InPlaceCycle || !IsObjectBranch(branch, allowUnevaluated: ctx.Contributors.Count == 0))
        {
            return false;
        }

        ctx.Contributors.Add((branch, condition, polarity, -1, 0));
        if (branch.Ref.IsPresent && !Collect(ctx, nodes[branch.Ref.FastNode], condition, polarity))
        {
            return false;
        }

        if (branch.AllOf is ChildRef[] allOf)
        {
            foreach (ChildRef child in allOf)
            {
                if (!Collect(ctx, nodes[child.FastNode], condition, polarity))
                {
                    return false;
                }
            }
        }

        if (branch.OneOf is ChildRef[] oneOf && !CollectAlternative(ctx, oneOf, condition, polarity, exactlyOne: true, discriminated: branch.OneOfDiscriminator is not null || branch.OneOfTypeUnion != TypeMask.None))
        {
            return false;
        }

        if (branch.AnyOf is ChildRef[] anyOf && !CollectAlternative(ctx, anyOf, condition, polarity, exactlyOne: false, discriminated: branch.AnyOfDiscriminator is not null || branch.AnyOfTypeUnion != TypeMask.None))
        {
            return false;
        }

        if (branch.Dependencies is DependencyEntry[] dependencies)
        {
            foreach (DependencyEntry dependency in dependencies)
            {
                if (ctx.Conditions.Count >= MaxConditions)
                {
                    return false;
                }

                int id = ctx.Conditions.Count;
                ctx.Conditions.Add(new PendingCondition { DependencyName = dependency.Name, Gate = condition, GatePolarity = polarity });
                if (dependency.RequiredNames.Length > 0)
                {
                    ctx.Extras.Add((id, true, dependency.RequiredNames));
                }

                if (dependency.Schema.IsPresent && !Collect(ctx, nodes[dependency.Schema.FastNode], id, true))
                {
                    return false;
                }
            }
        }

        if (branch.If.IsPresent)
        {
            SchemaNode test = nodes[branch.If.FastNode];
            if (test.AlwaysTrue)
            {
                return !branch.Then.IsPresent || Collect(ctx, nodes[branch.Then.FastNode], condition, polarity);
            }

            if (test.AlwaysFalse)
            {
                return !branch.Else.IsPresent || Collect(ctx, nodes[branch.Else.FastNode], condition, polarity);
            }

            if (!IsSupportedCondition(nodes, test) || ctx.Conditions.Count >= MaxConditions)
            {
                return false;
            }

            int id = ctx.Conditions.Count;
            ctx.Conditions.Add(new PendingCondition { Test = test, Gate = condition, GatePolarity = polarity });

            // When the condition holds, the if schema's own properties count as evaluated (its annotations are
            // kept), so it contributes under the same condition as then.
            if (test.Properties is not null && !Collect(ctx, test, id, true))
            {
                return false;
            }

            if (branch.Then.IsPresent && !Collect(ctx, nodes[branch.Then.FastNode], id, true))
            {
                return false;
            }

            if (branch.Else.IsPresent && !Collect(ctx, nodes[branch.Else.FastNode], id, false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A <c>oneOf</c>/<c>anyOf</c> fuses only when every branch is a plain <c>required</c> list (with at most a
    /// <c>type: object</c>), which the seen bits decide after the pass.
    /// </summary>
    private static bool CollectAlternative(CollectContext ctx, ChildRef[] branches, int condition, bool polarity, bool exactlyOne, bool discriminated)
    {
        var collected = new List<byte[][]>(branches.Length);
        bool requiredOnly = true;
        foreach (ChildRef child in branches)
        {
            SchemaNode branch = ctx.Nodes[child.FastNode];
            if (branch.RequiredNames is not byte[][] { Length: > 0 } required || !IsRequiredListOnly(branch))
            {
                requiredOnly = false;
                break;
            }

            collected.Add(required);
        }

        if (requiredOnly)
        {
            ctx.Alternatives.Add((condition, polarity, exactlyOne, [.. collected]));
            return true;
        }

        // Branches with object keywords: each becomes a contributor of a group, unconditional and without further
        // in-place applicators, so that a failure marks the branch and the group is counted after the pass. The pass
        // applies every branch that knows a name to that property, where the general path stops at the first branch
        // that passes, so a name with an expensive child in more than one branch is refused; and a keyword the general
        // path decides by discriminator or type union stays with it.
        if (!ctx.AllowAltGroups || discriminated || condition >= 0 || ctx.AltGroups.Count >= MaxAltGroups || branches.Length > 64
            || !ExpensiveChildrenAreDisjoint(ctx.Nodes, branches))
        {
            return false;
        }

        int group = ctx.AltGroups.Count;
        for (int b = 0; b < branches.Length; b++)
        {
            SchemaNode branch = ctx.Nodes[branches[b].FastNode];
            if (branch.AlwaysTrue || branch.AlwaysFalse || branch.InPlaceCycle || branch.HasInPlaceApplicators || branch.Dependencies is not null
                || !IsObjectBranch(branch, allowUnevaluated: false) || ctx.Contributors.Count >= MaxContributors)
            {
                return false;
            }

            ctx.Contributors.Add((branch, -1, true, group, b));
        }

        ctx.AltGroups.Add((exactlyOne, branches.Length));
        return true;
    }

    /// <summary>
    /// Whether no property name gets an expensive child (anything but a leaf, a simple array or a boolean schema)
    /// from more than one branch; pattern and additional properties count as every name.
    /// </summary>
    private static bool ExpensiveChildrenAreDisjoint(SchemaNode[] nodes, ChildRef[] branches)
    {
        static bool Cheap(SchemaNode n) => n.IsLeaf || n.IsSimpleArray || n.AlwaysTrue || n.AlwaysFalse;

        var expensiveBy = new Dictionary<string, int>(StringComparer.Ordinal);
        int expensiveWildcards = 0;
        foreach (ChildRef child in branches)
        {
            SchemaNode branch = nodes[child.FastNode];
            if (branch.Properties is Utf8NameMap<PropertyEntry> properties)
            {
                foreach (PropertyEntry entry in properties.Values)
                {
                    if (entry.Schema.IsPresent && !Cheap(nodes[entry.Schema.FastNode]))
                    {
                        string name = System.Text.Encoding.UTF8.GetString(entry.Name);
                        expensiveBy[name] = (expensiveBy.TryGetValue(name, out int n) ? n : 0) + 1;
                        if (expensiveBy[name] > 1)
                        {
                            return false;
                        }
                    }
                }
            }

            bool wildcard = false;
            if (branch.PatternProperties is PatternPropertyEntry[] patterns)
            {
                foreach (PatternPropertyEntry pattern in patterns)
                {
                    wildcard |= !Cheap(nodes[pattern.Schema.FastNode]);
                }
            }

            wildcard |= branch.AdditionalProperties.IsPresent && !Cheap(nodes[branch.AdditionalProperties.FastNode]);
            if (wildcard && ++expensiveWildcards > 1)
            {
                return false;
            }
        }

        return expensiveWildcards == 0 || expensiveBy.Count == 0;
    }

    private static bool IsRequiredListOnly(SchemaNode node)
    {
        if (node.HasConst || node.Enum is not null || node.HasNumberKeywords || node.HasStringKeywords || node.HasArrayKeywords || node.HasInPlaceApplicators
            || node.DynamicRef is not null || node.UnevaluatedProperties.IsPresent || node.UnevaluatedItems.IsPresent)
        {
            return false;
        }

        if (node.HasType && (node.Type & TypeMask.Object) == 0)
        {
            return false;
        }

        if (node.PatternProperties is not null || node.AdditionalProperties.IsPresent || node.PropertyNames.IsPresent
            || node.Dependencies is not null || node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            return false;
        }

        // The compiler registers every required name as a property entry without a schema.
        if (node.Properties is Utf8NameMap<PropertyEntry> properties)
        {
            foreach (PropertyEntry entry in properties.Values)
            {
                if (entry.Schema.IsPresent)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// A condition the plan can decide from the pass alone: <c>required</c> names, and <c>properties</c> whose
    /// schemas are constants or enums of strings, canonical integers or booleans (a value test), optionally with
    /// <c>type: object</c>, which the object plan has already established. At least one of the two must be present.
    /// </summary>
    private static bool IsSupportedCondition(SchemaNode[] nodes, SchemaNode test)
    {
        bool anyTest = test.RequiredNames is byte[][] { Length: > 0 };
        if (test.HasConst || test.Enum is not null || test.HasNumberKeywords || test.HasStringKeywords || test.HasArrayKeywords || test.HasInPlaceApplicators)
        {
            return false;
        }

        if (test.HasType && (test.Type & TypeMask.Object) == 0)
        {
            return false;
        }

        if (test.PatternProperties is not null || test.AdditionalProperties.IsPresent || test.PropertyNames.IsPresent
            || test.UnevaluatedProperties.IsPresent || test.Dependencies is not null || test.MinProperties >= 0 || test.MaxProperties >= 0)
        {
            return false;
        }

        if (test.Properties is Utf8NameMap<PropertyEntry> properties)
        {
            foreach (PropertyEntry entry in properties.Values)
            {
                if (!entry.Schema.IsPresent)
                {
                    continue;
                }

                if (ValueTestFor(nodes[entry.Schema.FastNode]) is null)
                {
                    return false;
                }

                anyTest = true;
            }
        }

        return anyTest;
    }

    /// <summary>
    /// The value test for a property schema inside an <c>if</c>: a keyable <c>const</c> or <c>enum</c> (strings,
    /// canonical integers, booleans), or a <c>pattern</c>, each with at most a <c>type</c>; null for anything else.
    /// </summary>
    private static FusedValueTest? ValueTestFor(SchemaNode schema)
    {
        if (schema.HasNumberKeywords || schema.HasObjectKeywords || schema.HasArrayKeywords || schema.HasInPlaceApplicators
            || schema.UnevaluatedProperties.IsPresent || schema.UnevaluatedItems.IsPresent || schema.AlwaysFalse)
        {
            return null;
        }

        if (schema.Pattern is PatternMatcher pattern)
        {
            if (schema.HasConst || schema.Enum is not null || schema.MinLength >= 0 || schema.MaxLength >= 0 || schema.Format != FormatKind.None || schema.Content != ContentKind.None
                || (schema.HasType && schema.Type != TypeMask.String))
            {
                return null;
            }

            return new FusedValueTest { Pattern = pattern, RequiresString = schema.HasType };
        }

        if (schema.HasStringKeywords)
        {
            return null;
        }

        var keys = new List<KeyValuePair<byte[], object>>();
        if (schema.HasConst)
        {
            if (!SchemaCompiler.TryGetDiscriminatorKey(schema.Const, out byte[]? key))
            {
                return null;
            }

            keys.Add(new KeyValuePair<byte[], object>(key!, SchemaCompiler.EnumSentinel));
        }
        else if (schema.Enum is ConstantValue[] values && values.Length > 0)
        {
            foreach (ConstantValue value in values)
            {
                if (!SchemaCompiler.TryGetDiscriminatorKey(value, out byte[]? key))
                {
                    return null;
                }

                keys.Add(new KeyValuePair<byte[], object>(key!, SchemaCompiler.EnumSentinel));
            }
        }
        else
        {
            return null;
        }

        return new FusedValueTest { Allowed = new Utf8NameMap<object>(keys) };
    }

    /// <summary>
    /// A branch whose only effect on an object instance is through object keywords and the fusable in-place applicators.
    /// </summary>
    private static bool IsObjectBranch(SchemaNode node, bool allowUnevaluated)
    {
        if (node.HasConst || node.Enum is not null || node.HasNumberKeywords || node.HasStringKeywords)
        {
            return false;
        }

        if (node.HasType && (node.Type & TypeMask.Object) == 0)
        {
            return false;
        }

        // anyOf, oneOf and dependencies are accepted here and validated by Collect (required-only branches, gated conditions).
        if (node.Not.IsPresent || node.PropertyNames.IsPresent || node.DynamicRef is not null)
        {
            return false;
        }

        if (!allowUnevaluated && node.UnevaluatedProperties.IsPresent)
        {
            return false;
        }

        return true;
    }
}