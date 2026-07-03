// <copyright file="PyIfThenElseHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// if/then/else: `if` selects the branch (evaluated in boolean mode; its evaluations count only when it
// matches). The taken branch (then/else) is in-place and shares the tracker. A branch with no generated
// validator is skipped (mirrors the TS `EvalName(...) is not null` guards).
internal sealed class PyIfThenElseHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 650;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ITernaryIfValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        SingleSubschemaKeywordTypeDeclaration? ifType = td.IfSubschemaType();
        if (ifType is null || PyEmit.EvalRef(mod, ifType.ReducedType) is not string ifRef)
        {
            return;
        }

        SingleSubschemaKeywordTypeDeclaration? thenType = td.ThenSubschemaType();
        SingleSubschemaKeywordTypeDeclaration? elseType = td.ElseSubschemaType();

        mod.Body.Append("    _ift = ").Append(mod.Rt("fresh")).Append("()\n");
        mod.Body.Append("    if ").Append(ifRef).Append("(value, _ift, None):\n");
        mod.Body.Append("        ev.merge_props(_ift)\n        ev.merge_items(_ift)\n");
        if (thenType is not null && PyEmit.EvalRef(mod, thenType.ReducedType) is string thenRef)
        {
            EmitBranch(mod, thenRef, thenType.ReducedType, "then", 8);
        }

        mod.Body.Append("    else:\n");
        if (elseType is not null && PyEmit.EvalRef(mod, elseType.ReducedType) is string elseRef)
        {
            EmitBranch(mod, elseRef, elseType.ReducedType, "else", 8);
        }
        else
        {
            mod.Body.Append("        pass\n");
        }
    }

    private static void EmitBranch(PyModule mod, string evalRef, TypeDeclaration branchType, string keyword, int indent)
    {
        string pad = new(' ', indent);
        mod.Body.Append(pad).Append("_bt = ").Append(mod.Rt("fresh")).Append("()\n");
        PyEmit.EmitDescentCall(mod, evalRef, "value", "_bt", "\"\"", PyEmit.Str("/" + keyword), indent);
        mod.Body.Append(pad).Append("if not _c:\n");
        PyEmit.Propagate(mod, indent + 4);
        mod.Body.Append(pad).Append("ev.merge_props(_bt)\n").Append(pad).Append("ev.merge_items(_bt)\n");
    }
}