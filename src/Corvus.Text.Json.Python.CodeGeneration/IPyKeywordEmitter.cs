// <copyright file="IPyKeywordEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

/// <summary>
/// Emits the Python validation code for a single JSON Schema keyword.
/// </summary>
/// <remarks>
/// <para>
/// Handlers match on the keyword's CAPABILITY INTERFACES (ICoreTypeValidationKeyword,
/// INumberConstantValidationKeyword, ...), never on the keyword text, and read constraints through
/// those interfaces (AllowedCoreTypes, TryGetOperator, TryGetValidationConstants, ...). This is
/// vocabulary-independent: one handler serves draft 4/6/7/2019-09/2020-12 even where the keyword name
/// or shape differs. The capability-to-handler mapping is shared verbatim with the TypeScript engine.
/// </para>
/// <para>
/// Emitters append into the module context's body at the validator's statement indent (4 spaces for a
/// top-level check), reading and registering the runtime / sibling imports they need on the context. The
/// module-per-type shape means a cross-type child validator is referenced qualified via
/// <see cref="PyEmit.EvalRef(PyModule, TypeDeclaration)"/>.
/// </para>
/// </remarks>
internal interface IPyKeywordEmitter
{
    /// <summary>
    /// Emits the Python validation code for the given keyword into the module context.
    /// </summary>
    /// <param name="mod">The module emission context (body + imports).</param>
    /// <param name="td">The type declaration being emitted.</param>
    /// <param name="keyword">The keyword whose validation is being emitted.</param>
    void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword);
}