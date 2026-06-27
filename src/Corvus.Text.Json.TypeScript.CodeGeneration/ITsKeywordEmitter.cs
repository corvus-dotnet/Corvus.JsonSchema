// <copyright file="ITsKeywordEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

/// <summary>
/// Emits the TypeScript validation code for a single JSON Schema keyword.
/// </summary>
/// <remarks>
/// <para>
/// Handlers match on the keyword's CAPABILITY INTERFACES (ICoreTypeValidationKeyword,
/// INumberConstantValidationKeyword, ...), never on the keyword text, and read constraints through
/// those interfaces (AllowedCoreTypes, TryGetOperator, TryGetValidationConstants, ...). This is
/// vocabulary-independent: one handler serves draft 4/6/7/2019-09/2020-12 even where the keyword name
/// or shape differs (e.g. draft-4 boolean exclusiveMinimum maps to the same Operator).
/// </para>
/// <para>
/// Public so external (consumer-supplied) validation handlers can participate in the emit dispatch —
/// the extensibility seam: register an <c>IKeywordValidationHandler</c> that also implements
/// <see cref="ITsKeywordEmitter"/>.
/// </para>
/// </remarks>
public interface ITsKeywordEmitter
{
    /// <summary>
    /// Emits the TypeScript validation code for the given keyword.
    /// </summary>
    /// <param name="sb">The builder accumulating the generated TypeScript source.</param>
    /// <param name="td">The type declaration being emitted.</param>
    /// <param name="keyword">The keyword whose validation is being emitted.</param>
    void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword);
}