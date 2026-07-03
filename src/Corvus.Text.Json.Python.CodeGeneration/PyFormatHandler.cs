// <copyright file="PyFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// format assertion: a standard string format validated via the runtime `fmt`; the sub-64-bit integer formats
// and the bounded float formats validated by an inline numeric-range check. Only registered when format
// assertion is requested; the 2020-12 default leaves format an annotation.
internal sealed class PyFormatHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
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

    private static readonly IReadOnlyDictionary<string, (double Min, double Max)> FloatFormatRanges =
        new Dictionary<string, (double, double)>(StringComparer.Ordinal)
        {
            ["half"] = (-65504, 65504),
            ["decimal"] = (-7.922816251426434e28, 7.922816251426434e28),
        };

    public uint ValidationHandlerPriority => 550;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IFormatProviderKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (!((IFormatProviderKeyword)keyword).TryGetFormat(td, out string? format) || string.IsNullOrEmpty(format))
        {
            return;
        }

        if (IntegerFormatRanges.TryGetValue(format!, out (long Min, long Max) range))
        {
            string min = PyEmit.Str(range.Min.ToString(CultureInfo.InvariantCulture));
            string max = PyEmit.Str(range.Max.ToString(CultureInfo.InvariantCulture));
            string cond = mod.Rt("is_num") + "(value) and (not " + mod.Rt("is_int") + "(value) or " + mod.Rt("cmp") + "(value, " + min + ") < 0 or " + mod.Rt("cmp") + "(value, " + max + ") > 0)";
            PyEmit.Check(mod, td, keyword.Keyword, cond, 4);
        }
        else if (FloatFormatRanges.TryGetValue(format!, out (double Min, double Max) frange))
        {
            string min = PyEmit.Str(frange.Min.ToString("R", CultureInfo.InvariantCulture));
            string max = PyEmit.Str(frange.Max.ToString("R", CultureInfo.InvariantCulture));
            string cond = mod.Rt("is_num") + "(value) and (" + mod.Rt("cmp") + "(value, " + min + ") < 0 or " + mod.Rt("cmp") + "(value, " + max + ") > 0)";
            PyEmit.Check(mod, td, keyword.Keyword, cond, 4);
        }
        else
        {
            string cond = "isinstance(value, str) and not " + mod.Rt("fmt") + "(" + PyEmit.Str(format!) + ", value)";
            PyEmit.Check(mod, td, keyword.Keyword, cond, 4);
        }
    }
}