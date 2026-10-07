// <copyright file="NodeValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;

/// <summary>
/// A generated method for one schema node, in flag mode: the interpreter's calling convention, so generated code and
/// the interpreter call each other without adapters.
/// </summary>
/// <param name="state">The evaluation state (raw rows and text, dynamic scope, depth).</param>
/// <param name="doc">The document.</param>
/// <param name="index">The value's row index.</param>
/// <returns>Whether the value is valid against the node.</returns>
internal delegate bool NodeValidator(ref EvaluationState state, IJsonDocument doc, int index);
#endif