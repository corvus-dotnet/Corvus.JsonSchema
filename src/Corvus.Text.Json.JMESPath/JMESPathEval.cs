// <copyright file="JMESPathEval.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// A compiled delegate that evaluates a JMESPath expression (or sub-expression)
/// against a JSON data context.
/// </summary>
/// <param name="data">The current data context.</param>
/// <param name="workspace">The workspace for intermediate document allocation.</param>
/// <returns>The resulting JSON element, or <see cref="JsonElement.Undefined"/> if the expression
/// resolves to no value.</returns>
internal delegate JsonElement JMESPathEval(in JsonElement data, JsonWorkspace workspace);
