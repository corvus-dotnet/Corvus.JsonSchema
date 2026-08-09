// <copyright file="SecurityLabelQueryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the security-label index plan (§14.4): the translation a backend uses when its query language
/// cannot express a <see cref="SecurityRule"/>, and the set algebra that resolves it to candidate row ids.
/// </summary>
/// <remarks>
/// The load-bearing test here is <see cref="The_plan_never_loses_a_row_the_evaluator_admits"/>. A plan is an
/// over-approximation, so it is allowed to return rows the rule denies (the caller evaluates exactly and
/// discards them) but never allowed to omit one it admits, which would silently hide a principal's own data.
/// That test is paired with <see cref="A_tenant_rule_narrows_to_the_tenants_rows"/>, because returning "no
/// narrowing" for every rule would satisfy soundness while doing nothing at all.
/// </remarks>
[TestClass]
public sealed class SecurityLabelQueryTests
{
    // The corpus spans single-value, multi-value, no-tag and unranked-value shapes, matching the rows the
    // store-conformance reach oracle uses so the two agree on what they are describing.
    private static readonly (string Id, SecurityTag[] Tags)[] Rows =
    [
        ("run-a", [new("tenant", "acme"), new("team", "payments")]),
        ("run-b", [new("tenant", "acme"), new("team", "hr")]),
        ("run-c", [new("tenant", "globex"), new("team", "payments")]),
        ("run-d", []),
        ("run-e", [new("tenant", "acme"), new("tenant", "beta")]),
        ("run-f", [new("classification", "public")]),
        ("run-g", [new("classification", "confidential")]),
        ("run-h", [new("classification", "restricted")]),
        ("run-i", [new("classification", "public"), new("classification", "restricted")]),
        ("run-j", [new("classification", "weird")]),
    ];

    private static readonly string[] RuleShapes =
    [
        "tenant == $claim.tenant",
        "tenant == 'acme'",
        "tenant != $claim.tenant",
        "tenant",
        "tenant && team == 'payments'",
        "tenant == 'acme' || team == 'hr'",
        "!(tenant == 'globex')",
        "tenant == $claim.missing",
        "'a' == 'a'",
        "team == team",
        "tenant == $claim.both",
        "$claims.intersects",
        "$claims.superset",
        "!$claims.superset",
        "$claims.intersects && tenant == $claim.tenant",
        "$claims.superset || team == 'payments'",
        "!($claims.intersects)",
        "tenant in ('acme', 'globex')",
        "classification in ('public', 'restricted')",
        "!(classification in ('restricted'))",
        "team in ('payments') && tenant in ('acme')",
    ];

    private static readonly string[] OrderedShapes =
    [
        "classification <= 'confidential'",
        "classification < 'confidential'",
        "classification >= 'confidential'",
        "classification > 'public'",
        "classification <= 'bogus'",
        "classification <= $claim.clearance",
        "classification >= $claim.clearance",
        "tenant <= 'acme'",
        "classification <= 'restricted' && tenant in ('acme')",
    ];

    [TestMethod]
    public async Task The_plan_never_loses_a_row_the_evaluator_admits()
    {
        var index = new FakeLabelIndex(Rows);

        foreach (string ruleText in RuleShapes)
        {
            await AssertSoundAsync(index, new SecurityFilter([SecurityRule.Compile(ruleText)], Claims()), ruleText);
        }

        foreach (string ruleText in OrderedShapes)
        {
            await AssertSoundAsync(index, new SecurityFilter([SecurityRule.Compile(ruleText, Classification())], Claims()), ruleText);
        }
    }

    [TestMethod]
    public async Task A_tenant_rule_narrows_to_the_tenants_rows()
    {
        var index = new FakeLabelIndex(Rows);
        var filter = new SecurityFilter([SecurityRule.Compile("tenant == 'acme'")], Claims());

        IReadOnlySet<string>? candidates = await ResolveAsync(index, filter);

        candidates.ShouldNotBeNull();
        candidates.OrderBy(x => x, StringComparer.Ordinal).ShouldBe(["run-a", "run-b", "run-e"]);
    }

    [TestMethod]
    public async Task A_rule_the_index_cannot_express_is_still_bounded_by_a_wrapper_rule_it_can()
    {
        // The deployment shell ANDs its mandated rule with the principal's (§14.3). The user half here is a
        // negation, which the index cannot narrow at all; the deployment half is an exact label. The result must
        // still be the tenant's rows, because that is the property tenant isolation rests on.
        var index = new FakeLabelIndex(Rows);
        var filter = new SecurityFilter(
            [SecurityRule.Compile("tenant == 'acme'"), SecurityRule.Compile("!(team == 'hr')")],
            Claims());

        IReadOnlySet<string>? candidates = await ResolveAsync(index, filter);

        candidates.ShouldNotBeNull("an un-narrowable user rule must not discard the wrapper rule's narrowing");
        candidates.OrderBy(x => x, StringComparer.Ordinal).ShouldBe(["run-a", "run-b", "run-e"]);
    }

    [TestMethod]
    public async Task No_narrowing_and_no_rows_are_different_answers()
    {
        var index = new FakeLabelIndex(Rows);

        // A negation the index cannot express: null, meaning "query as you would have anyway".
        (await ResolveAsync(index, new SecurityFilter([SecurityRule.Compile("!(tenant == 'globex')")], Claims())))
            .ShouldBeNull();

        // A label no row carries: an empty set, meaning "return nothing without querying". Conflating the two
        // would either scan for a rule that admits nothing, or return nothing for a rule that admits everything.
        IReadOnlySet<string>? none = await ResolveAsync(index, new SecurityFilter([SecurityRule.Compile("tenant == 'nobody'")], Claims()));
        none.ShouldNotBeNull();
        none.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task An_empty_rule_set_narrows_to_nothing_rather_than_to_everything()
    {
        // Deny-by-default (§14.2): an under-specified filter admits no row, and the plan must say so rather than
        // fall back to an unnarrowed scan whose rows the evaluator would then have to reject one by one.
        var index = new FakeLabelIndex(Rows);
        IReadOnlySet<string>? candidates = await ResolveAsync(index, new SecurityFilter([], Claims()));

        candidates.ShouldNotBeNull();
        candidates.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task An_intersection_stops_looking_up_once_it_is_empty()
    {
        var index = new FakeLabelIndex(Rows);
        var filter = new SecurityFilter(
            [SecurityRule.Compile("tenant == 'nobody'"), SecurityRule.Compile("team == 'payments'")],
            Claims());

        (await ResolveAsync(index, filter)).ShouldBeEmpty();

        // The second label is never looked up: nothing can re-enter an intersection that is already empty, and on
        // a real backend each lookup is a round trip.
        index.Lookups.ShouldNotContain(("team", "payments"));
    }

    [TestMethod]
    public async Task The_compiled_program_and_the_resolver_agree_on_every_rule_shape()
    {
        // Compile() is the seam for a backend that executes the set algebra natively (Redis) instead of
        // supplying point lookups. Its program must denote exactly the set the shared resolver computes —
        // including the null (no narrowing) versus empty (no rows) distinction — or the two consumption paths
        // would drift per backend, which is the failure §14.4 exists to prevent.
        var index = new FakeLabelIndex(Rows);

        List<(SecurityFilter Filter, string Rule)> corpus =
        [
            .. RuleShapes.Select(r => (new SecurityFilter([SecurityRule.Compile(r)], Claims()), r)),
            .. OrderedShapes.Select(r => (new SecurityFilter([SecurityRule.Compile(r, Classification())], Claims()), r)),
            (new SecurityFilter([], Claims()), "<empty rule set>"),
            (new SecurityFilter([SecurityRule.Compile("tenant == 'acme'"), SecurityRule.Compile("!(team == 'hr')")], Claims()), "<wrapper AND un-narrowable>"),
        ];

        foreach ((SecurityFilter filter, string ruleText) in corpus)
        {
            SecurityLabelQuery plan = filter.ToPredicate(SecurityLabelQueryEmitter.Instance);
            IReadOnlySet<string>? resolved = await SecurityLabelQueryResolver.ResolveAsync(plan, index, default);
            IReadOnlySet<string>? interpreted = Interpret(plan.Compile());

            if (resolved is null)
            {
                interpreted.ShouldBeNull($"rule '{ruleText}': the resolver said no-narrowing but the program narrowed");
            }
            else
            {
                interpreted.ShouldNotBeNull($"rule '{ruleText}': the resolver narrowed but the program said no-narrowing");
                interpreted.OrderBy(x => x, StringComparer.Ordinal)
                    .ShouldBe(resolved.OrderBy(x => x, StringComparer.Ordinal), $"rule '{ruleText}'");
            }
        }
    }

    // Executes a compiled program the way a set-native backend does — pushes look up a label set over the same
    // row corpus the FakeLabelIndex serves, operations pop and combine — so the differential test compares the
    // two consumption paths over identical data.
    private static IReadOnlySet<string>? Interpret(IReadOnlyList<SecurityLabelInstruction>? program)
    {
        if (program is null)
        {
            return null;
        }

        var stack = new Stack<HashSet<string>>();
        foreach (SecurityLabelInstruction instruction in program)
        {
            switch (instruction.Kind)
            {
                case SecurityLabelInstructionKind.PushLabel:
                    stack.Push(Rows.Where(r => r.Tags.Any(t => t.Key == instruction.Key && t.Value == instruction.Value)).Select(r => r.Id).ToHashSet(StringComparer.Ordinal));
                    break;

                case SecurityLabelInstructionKind.PushKey:
                    stack.Push(Rows.Where(r => r.Tags.Any(t => t.Key == instruction.Key)).Select(r => r.Id).ToHashSet(StringComparer.Ordinal));
                    break;

                case SecurityLabelInstructionKind.Union:
                    {
                        HashSet<string> accumulated = stack.Pop();
                        for (int i = 1; i < instruction.Count; ++i)
                        {
                            accumulated.UnionWith(stack.Pop());
                        }

                        stack.Push(accumulated);
                        break;
                    }

                default:
                    {
                        HashSet<string> accumulated = stack.Pop();
                        for (int i = 1; i < instruction.Count; ++i)
                        {
                            accumulated.IntersectWith(stack.Pop());
                        }

                        stack.Push(accumulated);
                        break;
                    }
            }
        }

        return stack.Count == 0 ? new HashSet<string>(StringComparer.Ordinal) : stack.Pop();
    }

    private static async ValueTask AssertSoundAsync(FakeLabelIndex index, SecurityFilter filter, string ruleText)
    {
        IReadOnlySet<string>? candidates = await ResolveAsync(index, filter);
        if (candidates is null)
        {
            return;
        }

        foreach ((string id, SecurityTag[] tags) in Rows)
        {
            if (filter.IsSatisfiedBy(tags))
            {
                candidates.ShouldContain(id, $"rule '{ruleText}' planned a candidate set that omits a row the evaluator admits");
            }
        }
    }

    private static ValueTask<IReadOnlySet<string>?> ResolveAsync(FakeLabelIndex index, SecurityFilter filter)
        => SecurityLabelQueryResolver.ResolveAsync(filter.ToPredicate(SecurityLabelQueryEmitter.Instance), index, default);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Claims()
        => new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["tenant"] = ["acme"],
            ["both"] = ["acme", "globex"],
            ["team"] = ["payments", "hr"],
            ["clearance"] = ["confidential"],
        };

    private static SecurityLabelOrderings Classification()
        => new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["classification"] = ["public", "internal", "confidential", "restricted"],
        });

    // Stands in for a backend's index, and records what was asked so a test can assert on lookups avoided.
    private sealed class FakeLabelIndex(IEnumerable<(string Id, SecurityTag[] Tags)> rows) : ISecurityLabelIndex
    {
        private readonly (string Id, SecurityTag[] Tags)[] rows = [.. rows];

        public List<(string Key, string Value)> Lookups { get; } = [];

        public ValueTask<IReadOnlyCollection<string>> LookupLabelAsync(string key, string value, CancellationToken cancellationToken)
        {
            this.Lookups.Add((key, value));
            return new ValueTask<IReadOnlyCollection<string>>(
                this.rows.Where(r => r.Tags.Any(t => t.Key == key && t.Value == value)).Select(r => r.Id).ToList());
        }

        public ValueTask<IReadOnlyCollection<string>> LookupKeyAsync(string key, CancellationToken cancellationToken)
        {
            this.Lookups.Add((key, "*"));
            return new ValueTask<IReadOnlyCollection<string>>(
                this.rows.Where(r => r.Tags.Any(t => t.Key == key)).Select(r => r.Id).ToList());
        }
    }
}