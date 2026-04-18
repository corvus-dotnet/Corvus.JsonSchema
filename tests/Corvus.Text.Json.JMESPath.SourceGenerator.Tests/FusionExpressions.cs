// <copyright file="FusionExpressions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JMESPath;

namespace Corvus.Text.Json.JMESPath.SourceGenerator.Tests;

// Two streaming stages

/// <summary>Source-generated evaluator for <c>[?v &gt; `1`] | [*].n</c>.</summary>
[JMESPathExpression("Expressions/filter-project.jmespath")]
public static partial class FilterProjectCG { }

/// <summary>Source-generated evaluator for <c>[] | [?@ &gt; `2`]</c>.</summary>
[JMESPathExpression("Expressions/flatten-filter.jmespath")]
public static partial class FlattenFilterCG { }

/// <summary>Source-generated evaluator for <c>[] | [*].x</c>.</summary>
[JMESPathExpression("Expressions/flatten-project.jmespath")]
public static partial class FlattenProjectCG { }

/// <summary>Source-generated evaluator for <c>[*].v | [?@ &gt; `1`]</c>.</summary>
[JMESPathExpression("Expressions/project-filter.jmespath")]
public static partial class ProjectFilterCG { }

// Streaming + Barrier

/// <summary>Source-generated evaluator for <c>[?v &gt; `1`] | sort_by(@, &amp;v)</c>.</summary>
[JMESPathExpression("Expressions/filter-sortby.jmespath")]
public static partial class FilterSortByCG { }

/// <summary>Source-generated evaluator for <c>[*].v | sort(@)</c>.</summary>
[JMESPathExpression("Expressions/project-sort.jmespath")]
public static partial class ProjectSortCG { }

/// <summary>Source-generated evaluator for <c>[*].v | reverse(@)</c>.</summary>
[JMESPathExpression("Expressions/project-reverse.jmespath")]
public static partial class ProjectReverseCG { }

/// <summary>Source-generated evaluator for <c>[] | sort(@)</c>.</summary>
[JMESPathExpression("Expressions/flatten-sort.jmespath")]
public static partial class FlattenSortCG { }

// Barrier + Streaming

/// <summary>Source-generated evaluator for <c>sort_by(@, &amp;v) | [*].n</c>.</summary>
[JMESPathExpression("Expressions/sortby-project.jmespath")]
public static partial class SortByProjectCG { }

/// <summary>Source-generated evaluator for <c>sort(@) | [?@ &gt; `2`]</c>.</summary>
[JMESPathExpression("Expressions/sort-filter.jmespath")]
public static partial class SortFilterCG { }

/// <summary>Source-generated evaluator for <c>reverse(@) | [*].n</c>.</summary>
[JMESPathExpression("Expressions/reverse-project.jmespath")]
public static partial class ReverseProjectCG { }

// Barrier + Barrier

/// <summary>Source-generated evaluator for <c>sort(@) | reverse(@)</c>.</summary>
[JMESPathExpression("Expressions/sort-reverse.jmespath")]
public static partial class SortReverseCG { }

/// <summary>Source-generated evaluator for <c>sort_by(@, &amp;v) | reverse(@)</c>.</summary>
[JMESPathExpression("Expressions/sortby-reverse.jmespath")]
public static partial class SortByReverseCG { }

// Three+ stages

/// <summary>Source-generated evaluator for <c>[?v &gt; `1`] | sort_by(@, &amp;v) | [*].{name: n, value: v}</c>.</summary>
[JMESPathExpression("Expressions/filter-sortby-hashproject.jmespath")]
public static partial class FilterSortByHashProjectCG { }

/// <summary>Source-generated evaluator for <c>[*].v | sort(@) | reverse(@)</c>.</summary>
[JMESPathExpression("Expressions/project-sort-reverse.jmespath")]
public static partial class ProjectSortReverseCG { }

/// <summary>Source-generated evaluator for <c>[] | [?@ &gt; `2`] | sort(@)</c>.</summary>
[JMESPathExpression("Expressions/flatten-filter-sort.jmespath")]
public static partial class FlattenFilterSortCG { }

/// <summary>Source-generated evaluator for <c>[?v &gt; `1`] | reverse(@) | [*].n</c>.</summary>
[JMESPathExpression("Expressions/filter-reverse-project.jmespath")]
public static partial class FilterReverseProjectCG { }

/// <summary>Source-generated evaluator for <c>sort_by(@, &amp;v) | reverse(@) | [*].{name: n}</c>.</summary>
[JMESPathExpression("Expressions/sortby-reverse-hashproject.jmespath")]
public static partial class SortByReverseHashProjectCG { }

// Terminal HashProject

/// <summary>Source-generated evaluator for <c>sort_by(@, &amp;v) | [*].{name: n, val: v}</c>.</summary>
[JMESPathExpression("Expressions/sortby-hashproject.jmespath")]
public static partial class SortByHashProjectCG { }

/// <summary>Source-generated evaluator for <c>reverse(@) | [*].{name: n}</c>.</summary>
[JMESPathExpression("Expressions/reverse-hashproject.jmespath")]
public static partial class ReverseHashProjectCG { }

/// <summary>Source-generated evaluator for <c>[?v &gt; `1`] | [*].{name: n, val: v}</c>.</summary>
[JMESPathExpression("Expressions/filter-hashproject.jmespath")]
public static partial class FilterHashProjectCG { }

// Non-terminal HashProject

/// <summary>Source-generated evaluator for <c>[*].{name: n, val: v} | reverse(@)</c>.</summary>
[JMESPathExpression("Expressions/hashproject-reverse.jmespath")]
public static partial class HashProjectReverseCG { }

// MapExpr

/// <summary>Source-generated evaluator for <c>map(&amp;v, @) | sort(@)</c>.</summary>
[JMESPathExpression("Expressions/map-sort.jmespath")]
public static partial class MapSortCG { }

/// <summary>Source-generated evaluator for <c>map(&amp;v, @) | [?@ &gt; `1`]</c>.</summary>
[JMESPathExpression("Expressions/map-filter.jmespath")]
public static partial class MapFilterCG { }

// Edge cases

/// <summary>Source-generated evaluator for <c>[?v &gt; `100`] | sort_by(@, &amp;v) | [*].n</c>.</summary>
[JMESPathExpression("Expressions/empty-through-pipeline.jmespath")]
public static partial class EmptyThroughPipelineCG { }

/// <summary>Source-generated evaluator for <c>[?v &gt; `2`] | sort_by(@, &amp;v) | [*].n</c>.</summary>
[JMESPathExpression("Expressions/single-through-pipeline.jmespath")]
public static partial class SingleThroughPipelineCG { }

// Bail-out / recursive fusion

/// <summary>Source-generated evaluator for <c>[*].v | sort(@) | [-1]</c>.</summary>
[JMESPathExpression("Expressions/recursive-inner-fusion.jmespath")]
public static partial class RecursiveInnerFusionCG { }
