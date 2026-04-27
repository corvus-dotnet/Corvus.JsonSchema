// <copyright file="BookAuthors.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;

namespace Corvus.Text.Json.JsonPath.SourceGenerator.Tests;

/// <summary>
/// Extracts all book authors from a bookstore document.
/// </summary>
[JsonPathExpression("Expressions/book-authors.jsonpath")]
internal static partial class BookAuthors;
