// <copyright file="PyAnyOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// anyOf: the instance must validate against AT LEAST ONE member. Each matched branch's evaluations OR-merge
// into the tracker. Detailed mode (r is not None): each branch gets its OWN sub-collector so that, when the
// disjunction fails, every branch's sub-failures are surfaced alongside the composite /anyOf failure. The
// boolean hot path (r is None) keeps the sub-collectors null and short-circuits.
internal sealed class PyAnyOfHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAnyOfSubschemaValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        List<string> members = PyEmit.MemberEvals(mod, td.AnyOfCompositionTypes());
        if (members.Count == 0)
        {
            return;
        }

        mod.Body.Append("    _anysubs: list[").Append(mod.Rt("Results")).Append("] | None = None if r is None else []\n");
        mod.Body.Append("    _anyok = False\n");
        int i = 0;
        foreach (string evalRef in members)
        {
            mod.Body.Append("    _anyt = ").Append(mod.Rt("fresh")).Append("()\n");
            mod.Body.Append("    _anyrb = None if r is None else r.fork()\n");
            PyEmit.EmitDescentCall(mod, evalRef, "value", "_anyt", "\"\"", PyEmit.Str("/" + keyword.Keyword + "/" + i), 4, "_anyrb");
            mod.Body.Append("    if _c:\n");
            mod.Body.Append("        ev.merge_props(_anyt)\n        ev.merge_items(_anyt)\n        _anyok = True\n");
            mod.Body.Append("    elif _anyrb is not None and _anysubs is not None:\n        _anysubs.append(_anyrb)\n");
            i++;
        }

        mod.Body.Append("    if not _anyok:\n");
        mod.Body.Append("        if r is None:\n            return False\n");
        mod.Body.Append("        if _anysubs is not None:\n            for _s in _anysubs:\n                r.merge(_s)\n");
        mod.Body.Append("        r.fail(").Append(PyEmit.Str("/" + keyword.Keyword)).Append(", ").Append(PyEmit.Str(PyEmit.AbsoluteKeywordLocation(td, keyword.Keyword))).Append(")\n");
        mod.Body.Append("        ok = False\n");
    }
}