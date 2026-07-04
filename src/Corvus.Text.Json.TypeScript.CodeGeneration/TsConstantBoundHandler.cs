// <copyright file="TsConstantBoundHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// minimum/maximum/exclusive*/multipleOf + minLength/maxLength + minItems/maxItems + min/maxProperties:
// all expose a constant + an Operator, so one handler covers them all across every draft.
internal sealed class TsConstantBoundHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword)
        => keyword is INumberConstantValidationKeyword or IStringLengthConstantValidationKeyword
            or IArrayLengthConstantValidationKeyword or IPropertyCountConstantValidationKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        // Numeric bounds/multipleOf: evaluated EXACTLY on the number's text via BigInt (§4.1), passing the
        // schema operand's source literal and comparing against String(value) -- no lossy double math.
        if (keyword is INumberConstantValidationKeyword num)
        {
            if (!num.TryGetOperator(td, out Operator nop) || !num.TryGetValidationConstants(td, out JsonElement[]? nconsts) || nconsts.Length == 0)
            {
                return;
            }

            string? ncond = TsEmit.NumericFailCondition(nop, TsEmit.Str(nconsts[0].GetRawText()));
            if (ncond is not null)
            {
                sb.Append("  if (__isNum(value) && ").Append(ncond).Append(") { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }

            return;
        }

        // Length/count bounds compare small safe integers -> plain JS is exact and faster.
        var integer = (IIntegerConstantValidationKeyword)keyword;
        if (!integer.TryGetOperator(td, out Operator op) || !integer.TryGetValidationConstants(td, out JsonElement[]? consts) || consts.Length == 0)
        {
            return;
        }

        (string guard, string left) = keyword switch
        {
            IStringLengthConstantValidationKeyword => ("typeof value === \"string\"", "[...value].length"),
            IArrayLengthConstantValidationKeyword => ("Array.isArray(value)", "value.length"),
            _ => ("__isObj(value)", "Object.keys(value).length"),
        };

        string? cond = TsEmit.FailCondition(op, left, consts[0].GetRawText());
        if (cond is not null)
        {
            sb.Append("  if (").Append(guard).Append(" && ").Append(cond).Append(") { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
        }
    }
}