// <copyright file="PyAllOfHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// allOf: the instance must validate against EVERY member. Each member gets its own tracker, merged into the
// parent after it validates. The member records its own failures (r is threaded through).
internal sealed class PyAllOfHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 600;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IAllOfSubschemaValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        List<string> members = PyEmit.MemberEvals(mod, td.AllOfCompositionTypes());
        if (members.Count == 0)
        {
            return;
        }

        int i = 0;
        foreach (string evalRef in members)
        {
            mod.Body.Append("    _allt = ").Append(mod.Rt("fresh")).Append("()\n");
            PyEmit.EmitDescentCall(mod, evalRef, "value", "_allt", "\"\"", PyEmit.Str("/" + keyword.Keyword + "/" + i), 4);
            mod.Body.Append("    if not _c:\n");
            PyEmit.Propagate(mod, 8);
            mod.Body.Append("    ev.merge_props(_allt)\n    ev.merge_items(_allt)\n");
            i++;
        }
    }
}