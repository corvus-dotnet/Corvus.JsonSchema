// <copyright file="Expressions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;

namespace JsonPath.Expressions;

/// <summary>
/// A JSONPath expression compiled at build time from all-authors.jsonpath.
/// Extracts all author names from any depth in the document using recursive descent.
/// </summary>
[JsonPathExpression("all-authors.jsonpath")]
public static partial class AllAuthors;
