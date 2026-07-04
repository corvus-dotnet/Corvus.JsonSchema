// <copyright file="TsTypeHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

internal sealed class TsTypeHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 100;

    public bool HandlesKeyword(IKeyword keyword) => keyword is ICoreTypeValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // A type declaration can carry SEVERAL core-type keywords (e.g. OpenAPI 3.0's `type` plus
        // `nullable`, which adds CoreTypes.Null). The dispatcher invokes this handler once per such
        // keyword, so we must emit the UNION of their allowed core types exactly once — otherwise each
        // keyword emits its own contradictory shape guard (`type:"string"` and `nullable:true` would
        // produce `if (!(typeof value === "string"))` AND `if (!(value === null))`, which rejects every
        // value). Emit on the FIRST core-type keyword only and use the declaration-level aggregate.
        if (!ReferenceEquals(keyword, FirstCoreTypeKeyword(td)))
        {
            return;
        }

        CoreTypes ct = td.AllowedCoreTypes();
        if (ct == CoreTypes.None || ct == CoreTypes.Any)
        {
            return;
        }

        var kinds = new List<string>();
        if (ct.HasFlag(CoreTypes.Object)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Object)); }
        if (ct.HasFlag(CoreTypes.Array)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Array)); }
        if (ct.HasFlag(CoreTypes.String)) { kinds.Add(TsEmit.KindExpr(CoreTypes.String)); }
        if (ct.HasFlag(CoreTypes.Number)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Number)); }
        else if (ct.HasFlag(CoreTypes.Integer)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Integer)); }
        if (ct.HasFlag(CoreTypes.Boolean)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Boolean)); }
        if (ct.HasFlag(CoreTypes.Null)) { kinds.Add(TsEmit.KindExpr(CoreTypes.Null)); }

        if (kinds.Count > 0)
        {
            sb.Append("  if (!(").Append(string.Join(" || ", kinds)).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
        }
    }

    // The first core-type validation keyword on the declaration in keyword order; the single emit point
    // for the unioned shape guard (so secondary core-type keywords such as `nullable` are no-ops here).
    private static IKeyword? FirstCoreTypeKeyword(TypeDeclaration td)
    {
        foreach (IKeyword keyword in td.Keywords())
        {
            if (keyword is ICoreTypeValidationKeyword)
            {
                return keyword;
            }
        }

        return null;
    }
}