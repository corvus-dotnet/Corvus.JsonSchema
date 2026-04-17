// <copyright file="ComplexQueryCodeGen.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Source-generated evaluator for <c>people[?age > `25`] | sort_by(@, &amp;age) | [*].{name: name, age: age}</c>.
/// </summary>
[JMESPathExpression("Expressions/complex-query.jmespath")]
public static partial class ComplexQueryCodeGen
{
}
