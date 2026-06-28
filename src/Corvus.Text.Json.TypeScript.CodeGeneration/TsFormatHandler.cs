// <copyright file="TsFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.TypeScript.CodeGeneration;

// format assertion (design §5.5): every standard format validated on its value via the __fmt runtime
// (strings), an inline integer-range check (OpenAPI byte/sbyte/int16/int32/uint16/uint32), or an inline
// number-range check (half/decimal). Only registered by CreateWithFormatAssertion (the optional/format
// suite); the default leaves format an annotation. A value of the wrong JS type is always valid (format
// only constrains its own type); unknown formats (and the unbounded single/double) are not asserted
// (__fmt returns true; no range entry).
internal sealed class TsFormatHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    // Sub-64-bit integer formats -> inclusive [Min, Max] integer-and-range check on a number value. Mirrors
    // TypeScriptLanguageProvider.KnownIntegerFormats (the brand-alias side); 64-bit+ formats stay bigint and
    // are not range-checked here.
    private static readonly IReadOnlyDictionary<string, (long Min, long Max)> IntegerFormatRanges =
        new Dictionary<string, (long, long)>(StringComparer.Ordinal)
        {
            ["byte"] = (0, 255),
            ["sbyte"] = (-128, 127),
            ["int16"] = (-32768, 32767),
            ["uint16"] = (0, 65535),
            ["int32"] = (-2147483648, 2147483647),
            ["uint32"] = (0, 4294967295),
        };

    // Bounded floating-point / arbitrary-precision formats -> a RANGE check on a number value (no integer
    // requirement). Mirrors TypeScriptLanguageProvider.KnownFloatFormats; single/double are unbounded (the C#
    // cast saturates), so they have no entry here and are not asserted.
    private static readonly IReadOnlyDictionary<string, (double Min, double Max)> FloatFormatRanges =
        new Dictionary<string, (double, double)>(StringComparer.Ordinal)
        {
            ["half"] = (-65504, 65504),
            ["decimal"] = (-7.922816251426434e28, 7.922816251426434e28),
        };

    public uint ValidationHandlerPriority => 550;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IFormatProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IFormatProviderKeyword)keyword).TryGetFormat(td, out string? format) && !string.IsNullOrEmpty(format))
        {
            if (IntegerFormatRanges.TryGetValue(format!, out (long Min, long Max) range))
            {
                string min = range.Min.ToString(CultureInfo.InvariantCulture);
                string max = range.Max.ToString(CultureInfo.InvariantCulture);
                sb.Append("  if (typeof value === \"number\" && (!Number.isInteger(value) || value < ").Append(min).Append(" || value > ").Append(max).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }
            else if (FloatFormatRanges.TryGetValue(format!, out (double Min, double Max) frange))
            {
                string min = frange.Min.ToString("R", CultureInfo.InvariantCulture);
                string max = frange.Max.ToString("R", CultureInfo.InvariantCulture);
                sb.Append("  if (typeof value === \"number\" && (value < ").Append(min).Append(" || value > ").Append(max).Append(")) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }
            else
            {
                sb.Append("  if (typeof value === \"string\" && !__fmt(").Append(TsEmit.Str(format!)).Append(", value)) { ").Append(TsEmit.FailShape(td, keyword.Keyword)).Append(" }\n");
            }
        }
    }
}