// <copyright file="PyConstantBoundHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// minimum/maximum/exclusive*/multipleOf + minLength/maxLength + minItems/maxItems + min/maxProperties:
// all expose a constant + an Operator, so one handler covers them all across every draft.
internal sealed class PyConstantBoundHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword)
        => keyword is INumberConstantValidationKeyword or IStringLengthConstantValidationKeyword
            or IArrayLengthConstantValidationKeyword or IPropertyCountConstantValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        // Numeric bounds / multipleOf: exact on the number's decimal value via the runtime. The operand is
        // passed as a string literal so the runtime coerces it to an exact Decimal (no lossy float).
        if (keyword is INumberConstantValidationKeyword num)
        {
            if (!num.TryGetOperator(td, out Operator nop) || !num.TryGetValidationConstants(td, out JsonElement[]? nconsts) || nconsts.Length == 0)
            {
                return;
            }

            string? ncond = PyEmit.NumericFailCondition(nop, nconsts[0].GetRawText(), mod);
            if (ncond is not null)
            {
                PyEmit.Check(mod, td, keyword.Keyword, mod.Rt("is_num") + "(value) and " + ncond, 4);
            }

            return;
        }

        // Length / count bounds: plain integer compare against the code-point / element / key count.
        var integer = (IIntegerConstantValidationKeyword)keyword;
        if (!integer.TryGetOperator(td, out Operator op) || !integer.TryGetValidationConstants(td, out JsonElement[]? consts) || consts.Length == 0)
        {
            return;
        }

        (string guard, string left) = keyword switch
        {
            IStringLengthConstantValidationKeyword => ("isinstance(value, str)", "len(value)"),
            IArrayLengthConstantValidationKeyword => (mod.Rt("is_arr") + "(value)", "len(value)"),
            _ => (mod.Rt("is_obj") + "(value)", "len(value)"),
        };

        string? cond = PyEmit.LengthFailCondition(op, left, consts[0].GetRawText());
        if (cond is not null)
        {
            PyEmit.Check(mod, td, keyword.Keyword, guard + " and " + cond, 4);
        }
    }
}