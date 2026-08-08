// <copyright file="ISecurityRuleSqlEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The SQL specialisation of <see cref="ISecurityRulePredicateEmitter{TPredicate}"/>: a backend whose predicate is
/// a SQL <c>WHERE</c> fragment. It adds nothing of its own — it exists so a SQL backend can name the shape it
/// implements, and so <see cref="SecurityFilter.ToSqlPredicate"/> can be written without a type argument at the
/// twenty-odd call sites that build SQL.
/// </summary>
/// <remarks>
/// The fragments a SQL implementation returns are correlated <c>EXISTS</c> subqueries against <em>its</em>
/// security-tag table, referencing the outer row of the query the predicate is appended to (e.g.
/// <c>WorkflowRuns.RunId</c>), and <see cref="ISecurityRulePredicateEmitter{TPredicate}.Parameter"/> returns the
/// dialect's bound-parameter placeholder (e.g. <c>@s0</c>) rather than the value — no security-tag value or claim
/// value is ever concatenated into the SQL text.
/// </remarks>
public interface ISecurityRuleSqlEmitter : ISecurityRulePredicateEmitter<string>;