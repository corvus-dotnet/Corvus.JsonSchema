// <copyright file="JsonPathEval.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// A compiled delegate that evaluates a JSONPath query against JSON data.
/// The result is a JSON array containing the matched node list.
/// </summary>
/// <param name="root">The root document element (the <c>$</c> target).</param>
/// <param name="workspace">The workspace for intermediate document allocation.</param>
/// <returns>A JSON array element containing the matched nodes.</returns>
internal delegate JsonElement JsonPathEval(in JsonElement root, JsonWorkspace workspace);
