// <copyright file="PyUnevaluatedPropertiesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// unevaluatedProperties: every own property NOT already evaluated (locally or by a matched in-place
// applicator) must validate against the fallback. Reads the evaluation tracker, populated by the
// earlier-priority handlers; the highest priority ensures this runs last.
internal sealed class PyUnevaluatedPropertiesHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 900;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ILocalAndAppliedEvaluatedPropertyValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        FallbackObjectPropertyType? fb = td.LocalAndAppliedEvaluatedPropertyType();
        if (fb is null)
        {
            return;
        }

        if (PyEmit.EvalRef(mod, fb.ReducedType) is not string evalRef)
        {
            return;
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_obj")).Append("(value):\n");
        mod.Body.Append("        _i = -1\n");
        mod.Body.Append("        for _k in value:\n");
        mod.Body.Append("            _i += 1\n");
        mod.Body.Append("            if not ev.has_prop(_i):\n");
        PyEmit.EmitDescentCall(mod, evalRef, "value[_k]", PyEmit.SubEv(mod, fb.ReducedType), PyEmit.PropInstSeg, PyEmit.Str("/" + keyword.Keyword), 16);
        mod.Body.Append("                if not _c:\n");
        PyEmit.Propagate(mod, 20);
        mod.Body.Append("            ev.mark_prop(_i)\n");
    }
}