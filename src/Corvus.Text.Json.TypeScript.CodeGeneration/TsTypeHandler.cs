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
        CoreTypes ct = ((ICoreTypeValidationKeyword)keyword).AllowedCoreTypes(td);
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
}