// <copyright file="PyContainsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// contains: at least minContains (default 1) and at most maxContains items match the contains subschema.
// Matched items are marked evaluated (so unevaluatedItems credits them).
internal sealed class PyContainsHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 520;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IArrayContainsValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (!((IArrayContainsValidationKeyword)keyword).TryGetContainsItemType(td, out ArrayItemsTypeDeclaration? it) || it is null)
        {
            return;
        }

        if (PyEmit.EvalRef(mod, it.ReducedType) is not string evalRef)
        {
            return;
        }

        string min = "1";
        string? max = null;
        foreach (IKeyword k in td.Keywords())
        {
            if (k is IArrayContainsCountConstantValidationKeyword cc && cc.TryGetOperator(td, out Operator op) && cc.TryGetValidationConstants(td, out JsonElement[]? cs) && cs.Length > 0)
            {
                if (op == Operator.GreaterThanOrEquals)
                {
                    min = cs[0].GetRawText();
                }
                else if (op == Operator.LessThanOrEquals)
                {
                    max = cs[0].GetRawText();
                }
            }
        }

        mod.Body.Append("    if ").Append(mod.Rt("is_arr")).Append("(value):\n");
        mod.Body.Append("        _track = not ev.n\n");
        mod.Body.Append("        _n = 0\n");
        mod.Body.Append("        for _idx in range(len(value)):\n");
        mod.Body.Append("            if ").Append(evalRef).Append("(value[_idx], ").Append(mod.Rt("NOEV")).Append(", None):\n");
        mod.Body.Append("                _n += 1\n                if _track: ev.mark_item(_idx)\n");
        mod.Body.Append("        if _n < ").Append(min).Append(":\n");
        PyEmit.FailBody(mod, td, keyword.Keyword, 12);
        if (max is not null)
        {
            mod.Body.Append("        if _n > ").Append(max).Append(":\n");
            PyEmit.FailBody(mod, td, keyword.Keyword, 12);
        }
    }
}