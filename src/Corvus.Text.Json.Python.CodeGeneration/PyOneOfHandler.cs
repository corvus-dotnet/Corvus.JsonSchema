// <copyright file="PyOneOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// oneOf: the instance must validate against EXACTLY ONE member; the matching branch's evaluations are merged.
// Detailed mode: each branch gets its own sub-collector; the failed branches' sub-failures are surfaced only
// when the disjunction fails with zero matches (an over-match is not explained by branch failures).
internal sealed class PyOneOfHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IOneOfSubschemaValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        List<string> members = PyEmit.MemberEvals(mod, td.OneOfCompositionTypes());
        if (members.Count == 0)
        {
            return;
        }

        mod.Body.Append("    _onesubs: list[").Append(mod.Rt("Results")).Append("] | None = None if r is None else []\n");
        mod.Body.Append("    _onec = 0\n");
        mod.Body.Append("    _oneacc = ").Append(mod.Rt("fresh")).Append("()\n");
        int i = 0;
        foreach (string evalRef in members)
        {
            mod.Body.Append("    _onet = ").Append(mod.Rt("fresh")).Append("()\n");
            mod.Body.Append("    _onerb = None if r is None else r.fork()\n");
            PyEmit.EmitDescentCall(mod, evalRef, "value", "_onet", "\"\"", PyEmit.Str("/" + keyword.Keyword + "/" + i), 4, "_onerb");
            mod.Body.Append("    if _c:\n");
            mod.Body.Append("        _onec += 1\n        _oneacc.merge_props(_onet)\n        _oneacc.merge_items(_onet)\n");
            mod.Body.Append("    elif _onerb is not None and _onesubs is not None:\n        _onesubs.append(_onerb)\n");
            i++;
        }

        mod.Body.Append("    if _onec != 1:\n");
        mod.Body.Append("        if r is None:\n            return False\n");
        mod.Body.Append("        if _onesubs is not None and _onec == 0:\n            for _s in _onesubs:\n                r.merge(_s)\n");
        mod.Body.Append("        r.fail(").Append(PyEmit.Str("/" + keyword.Keyword)).Append(", ").Append(PyEmit.Str(PyEmit.AbsoluteKeywordLocation(td, keyword.Keyword))).Append(")\n");
        mod.Body.Append("        ok = False\n");
        mod.Body.Append("    ev.merge_props(_oneacc)\n    ev.merge_items(_oneacc)\n");
    }
}