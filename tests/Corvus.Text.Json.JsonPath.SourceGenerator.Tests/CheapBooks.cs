// <copyright file="CheapBooks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.JsonPath;

namespace Corvus.Text.Json.JsonPath.SourceGenerator.Tests;

/// <summary>
/// Extracts titles of books with price less than 10.
/// </summary>
[JsonPathExpression("Expressions/cheap-books.jsonpath")]
internal static partial class CheapBooks;
