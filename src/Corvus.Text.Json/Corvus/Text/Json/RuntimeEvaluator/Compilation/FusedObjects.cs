// <copyright file="FusedObjects.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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

    /// <summary>The <c>if</c> conditions, each a set of names that must be present.</summary>
    public FusedCondition[] Conditions = [];

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
}

/// <summary>One child schema applying to a property on behalf of a branch; a node of -1 covers without evaluation.</summary>
internal readonly struct FusedApplication(int contributor, int node)
{
    public readonly int Contributor = contributor;
    public readonly int Node = node;
}

/// <summary>A branch: its condition, what it does with names no entry knows, and what it requires.</summary>
internal sealed class FusedContributor
{
    public int Condition = -1;
    public bool Polarity = true;
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
}

/// <summary>Builds fused object plans over a compiled node graph.</summary>
internal static class FusedObjects
{
    private const int MaxNames = SchemaNode.InlineBitWords * 64;
    private const int MaxConditions = 64;
    private const int MaxContributors = 64;

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
        // Fusing pays when a pass over every instance property is unavoidable, which is what unevaluatedProperties
        // demands; for branches without it the general path's schema-driven lookups (unrolled properties) can beat a
        // pass over a large instance, so those are left alone.
        bool worthwhile = node.UnevaluatedProperties.IsPresent;
        if (!worthwhile || !IsObjectBranch(node, allowUnevaluated: true) || node.InPlaceCycle)
        {
            return null;
        }

        var contributors = new List<(SchemaNode Node, int Condition, bool Polarity)>();
        var conditions = new List<SchemaNode>();
        if (!Collect(nodes, node, -1, true, contributors, conditions))
        {
            return null;
        }

        if (contributors.Count > MaxContributors || conditions.Count > MaxConditions)
        {
            return null;
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

        foreach ((SchemaNode branch, _, _) in contributors)
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

        foreach (SchemaNode condition in conditions)
        {
            foreach (byte[] name in condition.RequiredNames!)
            {
                NameBit(name);
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
            (SchemaNode branch, int condition, bool polarity) = contributors[c];
            var contributor = new FusedContributor
            {
                Condition = condition,
                Polarity = polarity,
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

        fused.Contributors = built;

        var builtConditions = new FusedCondition[conditions.Count];
        for (int i = 0; i < conditions.Count; i++)
        {
            byte[][] required = conditions[i].RequiredNames!;
            var bits = new int[required.Length];
            for (int j = 0; j < required.Length; j++)
            {
                bits[j] = NameBit(required[j]);
            }

            builtConditions[i] = new FusedCondition { RequiredBits = bits };
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
                    applications.Add(new FusedApplication(c, ApplicationNode(nodes, entry.Schema)));
                    anyConditional |= conditional;
                }

                if (branch.PatternProperties is PatternPropertyEntry[] patterns)
                {
                    foreach (PatternPropertyEntry pattern in patterns)
                    {
                        if (pattern.Matcher.IsMatch(name))
                        {
                            matched = true;
                            applications.Add(new FusedApplication(c, ApplicationNode(nodes, pattern.Schema)));
                            anyConditional |= conditional;
                        }
                    }
                }

                if (!matched && branch.AdditionalProperties.IsPresent)
                {
                    applications.Add(new FusedApplication(c, ApplicationNode(nodes, branch.AdditionalProperties)));
                    anyConditional |= conditional;
                }
            }

            var fusedEntry = new FusedEntry { Name = name, Index = i, Applications = [.. applications], AnyConditional = anyConditional };
            entries[i] = fusedEntry;
            mapEntries.Add(new KeyValuePair<byte[], FusedEntry>(name, fusedEntry));
        }

        fused.EntryList = entries;
        fused.Entries = new Utf8NameMap<FusedEntry>(mapEntries);
        return fused;
    }

    /// <summary>A child that is <c>true</c> covers the property without a call.</summary>
    private static int ApplicationNode(SchemaNode[] nodes, in ChildRef child)
    {
        return nodes[child.FastNode].AlwaysTrue ? -1 : child.FastNode;
    }

    /// <summary>
    /// Walks the in-place applicators of a branch, adding every object branch with its condition. Fails when a branch
    /// cannot be fused.
    /// </summary>
    private static bool Collect(SchemaNode[] nodes, SchemaNode branch, int condition, bool polarity, List<(SchemaNode, int, bool)> contributors, List<SchemaNode> conditions)
    {
        if (branch.AlwaysTrue)
        {
            return true;
        }

        if (branch.AlwaysFalse || branch.InPlaceCycle || !IsObjectBranch(branch, allowUnevaluated: contributors.Count == 0))
        {
            return false;
        }

        contributors.Add((branch, condition, polarity));

        if (branch.Ref.IsPresent && !Collect(nodes, nodes[branch.Ref.FastNode], condition, polarity, contributors, conditions))
        {
            return false;
        }

        if (branch.AllOf is ChildRef[] allOf)
        {
            foreach (ChildRef child in allOf)
            {
                if (!Collect(nodes, nodes[child.FastNode], condition, polarity, contributors, conditions))
                {
                    return false;
                }
            }
        }

        if (branch.If.IsPresent)
        {
            // One level of condition: a conditional branch may not introduce another.
            if (condition >= 0)
            {
                return false;
            }

            SchemaNode test = nodes[branch.If.FastNode];
            if (test.AlwaysTrue)
            {
                return !branch.Then.IsPresent || Collect(nodes, nodes[branch.Then.FastNode], condition, polarity, contributors, conditions);
            }

            if (test.AlwaysFalse)
            {
                return !branch.Else.IsPresent || Collect(nodes, nodes[branch.Else.FastNode], condition, polarity, contributors, conditions);
            }

            if (!IsRequiredOnly(test) || conditions.Count >= MaxConditions)
            {
                return false;
            }

            int id = conditions.Count;
            conditions.Add(test);
            if (branch.Then.IsPresent && !Collect(nodes, nodes[branch.Then.FastNode], id, true, contributors, conditions))
            {
                return false;
            }

            if (branch.Else.IsPresent && !Collect(nodes, nodes[branch.Else.FastNode], id, false, contributors, conditions))
            {
                return false;
            }
        }
        else if (branch.Then.IsPresent || branch.Else.IsPresent)
        {
            // then/else without if are ignored by the specification; nothing to fuse.
        }

        return true;
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

        if (node.AnyOf is not null || node.OneOf is not null || node.Not.IsPresent || node.Dependencies is not null || node.PropertyNames.IsPresent || node.DynamicRef is not null)
        {
            return false;
        }

        if (!allowUnevaluated && node.UnevaluatedProperties.IsPresent)
        {
            return false;
        }

        return true;
    }

    /// <summary>An <c>if</c> that is only a <c>required</c> list (and possibly <c>type: object</c>).</summary>
    private static bool IsRequiredOnly(SchemaNode test)
    {
        if (test.RequiredNames is not byte[][] { Length: > 0 })
        {
            return false;
        }

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
                if (entry.Schema.IsPresent)
                {
                    return false;
                }
            }
        }

        return true;
    }
}