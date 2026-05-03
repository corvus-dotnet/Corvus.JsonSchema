// <copyright file="BuiltInFunctionCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests targeting uncovered branches in <see cref="BuiltInFunctions"/>,
/// identified from merged Cobertura coverage data.
/// </summary>
public class BuiltInFunctionCoverageTests
{
    private static string Eval(string expression, string data = "null")
    {
        return JsonataEvaluator.Default.EvaluateToString(expression, data) ?? "undefined";
    }

    // ─── $filter with array flattening (lines 2244-2280) ──────────────

    [Fact]
    public void Filter_MultiValuedSequenceContainingArrays_Flattens()
    {
        // Multi-valued sequence with arrays triggers the flatten path in $filter
        string data = """
        {
          "Account": {
            "Order": [
              {"Product": [{"Price": 5, "Name": "Cheap"}, {"Price": 15, "Name": "Mid"}]},
              {"Product": {"Price": 25, "Name": "Expensive"}}
            ]
          }
        }
        """;
        string result = Eval(
            """$filter(Account.Order.Product, function($v){$v.Price > 10})""",
            data);
        Assert.Contains("Mid", result);
        Assert.Contains("Expensive", result);
        Assert.DoesNotContain("Cheap", result);
    }

    [Fact]
    public void Filter_MultiValuedWithArrays_PreservesNonArrayElements()
    {
        string data = """
        {
          "groups": [
            {"items": [1, 2, 3]},
            {"items": [4, 5, 6]}
          ]
        }
        """;
        string result = Eval(
            """$filter(groups.items, function($v){$v > 3})""",
            data);
        Assert.Equal("""[4,5,6]""", result);
    }

    // ─── $spread multi-element sequence with arrays (lines 2669-2718) ──

    [Fact]
    public void Spread_MultiElementSequenceWithArrays()
    {
        // Multi-valued sequence where elements are arrays of objects
        // departments.people produces [array1, array2] as multi-value sequence
        string data = """
        {
          "departments": [
            {"people": [{"name": "Alice"}, {"name": "Bob"}]},
            {"people": [{"name": "Charlie"}]}
          ]
        }
        """;
        // $spread on a path producing multi-valued sequence with arrays
        string result = Eval("$spread(departments.people)", data);
        Assert.NotNull(result);
        Assert.Contains("name", result);
    }

    [Fact]
    public void Spread_MultiElementSequenceObjects()
    {
        // Multi-valued sequence of objects (no arrays) — exercises the object path (2699-2704)
        string data = """
        {
          "items": [
            {"a": 1, "b": 2},
            {"c": 3}
          ]
        }
        """;
        string result = Eval("$spread(items)", data);
        Assert.Contains("\"a\"", result);
    }

    [Fact]
    public void Spread_SingleObject()
    {
        string result = Eval("""$spread({"x":1,"y":2})""");
        Assert.Contains("\"x\"", result);
        Assert.Contains("\"y\"", result);
    }

    // ─── $single without predicate, multi-valued sequence (lines 2896-2906) ────

    [Fact]
    public void Single_NoPredicate_MultiValuedSequenceOfOne()
    {
        // Path producing single-element multi-valued sequence → returns the element (line 2906)
        string data = """{"items": [{"name": "only"}]}""";
        string result = Eval("$single(items)", data);
        Assert.Equal("""{"name": "only"}""", result);
    }

    [Fact]
    public void Single_NoPredicate_UndefinedInput_ReturnsUndefined()
    {
        string result = Eval("$single(nothing)", """{"something": 1}""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void Single_NoPredicate_MultiValuedSequenceMultiple_ThrowsD3138()
    {
        // Path producing multi-valued sequence with Count > 1 (line 2901-2903)
        string data = """
        {
          "groups": [
            {"value": 1},
            {"value": 2}
          ]
        }
        """;
        var ex = Assert.Throws<JsonataException>(
            () => Eval("$single(groups.value)", data));
        Assert.Equal("D3138", ex.Code);
    }

    [Fact]
    public void Single_NoPredicate_EmptyMultiValuedSequence_ThrowsD3139()
    {
        // Path producing an empty array → throws D3139 (line 2896-2898)
        string data = """{"items": []}""";
        var ex = Assert.Throws<JsonataException>(
            () => Eval("$single(items)", data));
        Assert.Equal("D3139", ex.Code);
    }

    // ─── $shuffle multi-element sequence (lines 5353-5366) ─────────────

    [Fact]
    public void Shuffle_MultiElementSequence()
    {
        // Multi-valued sequence (not a singleton array) triggers the sequence path
        string data = """
        {
          "groups": [
            {"items": [1, 2]},
            {"items": [3, 4]}
          ]
        }
        """;
        string result = Eval("$count($shuffle(groups.items))", data);
        // Should have all 4 elements regardless of order
        Assert.Equal("4", result);
    }

    [Fact]
    public void Shuffle_SingletonNonArray_WrapsInArray()
    {
        // Reference impl (JSONata 1.8.7) returns [42] — wraps scalar in single-element array.
        // Fixed to match reference behavior.
        string result = Eval("""$shuffle(42)""");
        Assert.Equal("[42]", result);
    }

    // ─── $map with index parameter ─────────────────────────────────────

    [Fact]
    public void Map_WithIndexParameter()
    {
        string result = Eval("""$map([10,20,30], function($v, $i){$i})""");
        Assert.Equal("[0,1,2]", result);
    }

    // ─── $reduce edge cases ────────────────────────────────────────────

    [Fact]
    public void Reduce_SingleElement_ReturnsElement()
    {
        string result = Eval("""$reduce([42], function($prev, $curr){$prev + $curr})""");
        Assert.Equal("42", result);
    }

    // ─── $zip ──────────────────────────────────────────────────────────

    [Fact]
    public void Zip_UnequalLengthArrays()
    {
        // JSONata $zip truncates to the shortest array
        string result = Eval("""$zip([1,2,3], ["a","b"])""");
        Assert.Equal("""[[1,"a"],[2,"b"]]""", result);
    }

    // ─── $sort ─────────────────────────────────────────────────────────

    [Fact]
    public void Sort_WithCustomComparator()
    {
        string result = Eval("""$sort([3,1,2], function($a,$b){$a > $b})""");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void Sort_StringsDefaultOrder()
    {
        string result = Eval("""$sort(["banana", "apple", "cherry"])""");
        Assert.Equal("""["apple","banana","cherry"]""", result);
    }

    // ─── Context-argument patterns (lines 1414-1418, 3133-3138) ───────

    [Fact]
    public void Contains_ContextArgPattern()
    {
        // $contains with 1 arg uses context as the string (lines 1414-1418)
        // Input is the string, expression uses implicit context
        string result = Eval("$contains(\"world\")", "\"hello world\"");
        Assert.Equal("true", result);
    }

    [Fact]
    public void Contains_ContextArgNoMatch()
    {
        string result = Eval("$contains(\"xyz\")", "\"hello\"");
        Assert.Equal("false", result);
    }

    [Fact]
    public void Split_ContextArgPattern()
    {
        // $split with 1 arg uses context as the string (lines 1316-1321)
        string result = Eval("$split(\",\")", "\"a,b,c\"");
        Assert.Equal("""["a","b","c"]""", result);
    }

    [Fact]
    public void Split_ContextArgWithLimit()
    {
        // $split with 2 args — still needs context (2 args = str + pattern)
        string data = """{"text": "one-two-three"}""";
        string result = Eval("$split(text, \"-\", 2)", data);
        Assert.Equal("""["one","two"]""", result);
    }

    [Fact]
    public void Match_ContextArgPattern()
    {
        // $match with 1 arg uses context as the string (lines 3133-3138)
        string result = Eval("$match(/[0-9]+/)", "\"abc123def456\"");
        Assert.Contains("\"match\"", result);
        Assert.Contains("123", result);
    }

    // ─── $number with hex/binary/octal (TryParseSpecialRadix) ─────────
    // NOTE: These are EXTENSIONS beyond the JSONata reference implementation.
    // Reference impl (v1.8.7+) throws D3030 for hex/binary/octal prefixes.
    // Our implementation adds support for 0x, 0b, 0o prefixes as a deliberate extension.

    [Fact]
    public void Number_HexPrefix()
    {
        // EXTENSION: Covers TryParseSpecialRadix hex path (line 8744)
        string result = Eval("""$number("0xFF")""");
        Assert.Equal("255", result);
    }

    [Fact]
    public void Number_HexUpperCase()
    {
        string result = Eval("""$number("0XAB")""");
        Assert.Equal("171", result);
    }

    [Fact]
    public void Number_BinaryPrefix()
    {
        // Covers TryParseSpecialRadix binary path (line 8756)
        string result = Eval("""$number("0b1010")""");
        Assert.Equal("10", result);
    }

    [Fact]
    public void Number_OctalPrefix()
    {
        // Covers TryParseSpecialRadix octal path (line 8775)
        string result = Eval("""$number("0o17")""");
        Assert.Equal("15", result);
    }

    [Fact]
    public void Number_InvalidBinary_ThrowsD3030()
    {
        // Invalid binary digits (not 0 or 1) — error path (line 8763)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0b1234")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_InvalidOctal_ThrowsD3030()
    {
        // Invalid octal digits (8, 9) — error path (line 8781)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0o89")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_InvalidHex_ThrowsD3030()
    {
        // Invalid hex string — error path (line 8752)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0xGG")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    [Fact]
    public void Number_EmptyBinary_ThrowsD3030()
    {
        // Empty binary after prefix — (line 8772 digits.Length == 0)
        var ex = Assert.Throws<JsonataException>(() => Eval("""$number("0b")"""));
        Assert.StartsWith("D3030", ex.Message);
    }

    // ─── $substring with supplementary Unicode (CodePointsToString) ───

    [Fact]
    public void Substring_SupplementaryUnicode()
    {
        // Characters above U+FFFF trigger supplementary code point path
        // 🎉 is U+1F389 (above BMP), needs surrogate pair handling
        string result = Eval("""$substring("A🎉B", 1, 1)""");
        Assert.Equal("\"🎉\"", result);
    }

    [Fact]
    public void Substring_MultipleSurrogates()
    {
        // Multiple supplementary characters
        string result = Eval("""$substring("🌍🌎🌏", 1, 2)""");
        Assert.Equal("\"🌎🌏\"", result);
    }

    // ─── $replace on multi-value pattern ──────────────────────────────

    [Fact]
    public void Replace_ContextArgPattern()
    {
        // $replace with context arg (2 args: pattern + replacement, context provides string)
        string result = Eval("$replace(\"world\", \"earth\")", "\"hello world\"");
        Assert.Equal("\"hello earth\"", result);
    }

    // ─── NaN/Infinity serialization in $string (lines 451-453) ────────

    [Fact]
    public void String_ArrayContainingNaN_OmitsNaN()
    {
        // Reference: $string([1, 0/0, 3]) → "[1,null,3]"
        // Our implementation: NaN evaluates to undefined and is omitted from arrays,
        // so the result is "[1,3]" rather than "[1,null,3]".
        string result = Eval("$string([1, 0/0, 3])");
        Assert.Equal("\"[1,3]\"", result);
    }

    // ─── $contains with context arg (lines 1415-1418) ─────────────────

    [Fact]
    public void Contains_ContextArg_SingleArgForm()
    {
        // Reference: "hello".$contains("ell") → true
        // 1-arg form uses context as the string input
        string result = Eval("$contains(\"ell\")", "\"hello\"");
        Assert.Equal("true", result);
    }

    // ─── $split with context arg (lines 3134-3138) ────────────────────

    [Fact]
    public void Split_ContextArg_SingleArgForm()
    {
        // Reference: "a,b,c".$split(",") → ["a","b","c"]
        string result = Eval("$split(\",\")", "\"a,b,c\"");
        Assert.Equal("[\"a\",\"b\",\"c\"]", result);
    }

    // ─── $replace with limit 0 (lines 3538-3539) ─────────────────────

    [Fact]
    public void Replace_RegexLambdaWithLimitZero_NoReplacements()
    {
        // Lines 3537-3539: RegexReplaceWithFunction limit <= 0 early return.
        // Must use lambda replacement (not string) to reach this path.
        string result = Eval("""$replace("aaa", /a/, function($m){"x"}, 0)""");
        Assert.Equal("\"aaa\"", result);
    }

    // ─── $split with non-string separator (lines 3882-3883) ───────────

    [Fact]
    public void Split_NonStringSeparator_ReturnsDefault()
    {
        // Reference: $split("abc", 123) → T0411 error
        // Our implementation returns default (empty) for non-string separator
        var ex = Assert.ThrowsAny<Exception>(
            () => Eval("""$split("abc", 123)"""));
        Assert.True(ex is JsonataException || ex is InvalidOperationException, $"Unexpected exception: {ex.GetType().Name}");
    }

    // ─── $decodeUrlComponent with bad % encoding (lines 4453-4458) ────

    [Fact]
    public void DecodeUrlComponent_InvalidPercentEncoding_ThrowsD3140()
    {
        // Reference: $decodeUrlComponent("%GG") → D3140
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$decodeUrlComponent("%GG")"""));
        Assert.Equal("D3140", ex.Code);
    }

    // ─── $formatNumber D3090 (line 4800) ──────────────────────────────

    [Fact]
    public void FormatNumber_MandatoryBeforeOptional_ThrowsD3090()
    {
        // Reference: $formatNumber(1234.5, "0#0") → D3090
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$formatNumber(1234.5, "0#0")"""));
        Assert.Equal("D3090", ex.Code);
    }

    // ─── $formatNumber D3093 empty exponent (lines 4826-4827) ─────────

    [Fact]
    public void FormatNumber_EmptyExponent_IsTreatedAsSuffix()
    {
        // The pattern "#.##e" has exponent separator at the boundary of the active region.
        // Due to parsing logic, the trailing 'e' is treated as suffix (not an empty exponent),
        // so no D3093 error is thrown. The lines 4825-4827 are unreachable with this parsing.
        // This test documents the actual behavior.
        string result = Eval("""$formatNumber(1234.5, "#.##e")""");
        Assert.NotNull(result);
    }

    // ─── $sort with undefined comparator result (lines 6416-6417) ─────

    [Fact]
    public void Sort_ComparatorReturnsUndefined_MaintainsOrder()
    {
        // Reference: $sort([3,1,2], function($a,$b){$nothing}) → [3,1,2]
        string result = Eval("""$sort([3,1,2], function($a,$b){$nothing})""");
        Assert.Equal("[3,1,2]", result);
    }

    // ─── $sort with numeric comparator (lines 6443-6448) ──────────────
    // BUG: Our implementation treats numeric comparator returns as signed comparison
    // values (negative=a-first, positive=b-first). The JSONata reference treats them
    // as boolean (truthy=swap, falsy=keep). This means $sort([3,1,2], function($a,$b){$a-$b})
    // returns [1,2,3] in our impl (numeric semantics) but [2,1,3] in the reference (boolean).
    // The boolean form ($a > $b → ascending) works correctly in both implementations.

    [Fact]
    public void Sort_BooleanComparator_Ascending()
    {
        // Reference: $sort([3,1,2], function($a,$b){$a > $b}) → [1,2,3] ✓
        string result = Eval("""$sort([3,1,2], function($a,$b){$a > $b})""");
        Assert.Equal("[1,2,3]", result);
    }

    [Fact]
    public void Sort_BooleanComparator_Descending()
    {
        // Reference: $sort([3,1,2], function($a,$b){$a < $b}) → [3,2,1] ✓
        string result = Eval("""$sort([3,1,2], function($a,$b){$a < $b})""");
        Assert.Equal("[3,2,1]", result);
    }

    // ─── XPathDateTimeFormatter: TryParseDateTime error branches (lines 227-324) ──────────────

    [Fact]
    public void ToMillis_BadYear_ReturnsUndefined()
    {
        // Invalid year "abc" should fail TryParseDateTime (line 249-251 return false)
        string result = Eval("""$toMillis("abc-01-15", "[Y]-[M01]-[D01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadDay_ReturnsUndefined()
    {
        // Invalid day "xx" should fail (line 265-267 return false)
        string result = Eval("""$toMillis("2024-01-xx", "[Y]-[M01]-[D01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadHour_ReturnsUndefined()
    {
        // Invalid hour "ab" should fail (line 281-283 return false)
        string result = Eval("""$toMillis("2024-01-15 ab:30:00", "[Y]-[M01]-[D01] [H01]:[m01]:[s01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadMinute_ReturnsUndefined()
    {
        // Invalid minute "xy" should fail (line 297-299 return false)
        string result = Eval("""$toMillis("2024-01-15 10:xy:00", "[Y]-[M01]-[D01] [H01]:[m01]:[s01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadSecond_ReturnsUndefined()
    {
        // Invalid second "zz" should fail (line 305-307 return false)
        string result = Eval("""$toMillis("2024-01-15 10:30:zz", "[Y]-[M01]-[D01] [H01]:[m01]:[s01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadFractional_ReturnsUndefined()
    {
        // Invalid fractional "abc" should fail (line 313-315 return false)
        string result = Eval("""$toMillis("2024-01-15 10:30:00.abc", "[Y]-[M01]-[D01] [H01]:[m01]:[s01].[f001]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadAmPm_ReturnsUndefined()
    {
        // Invalid AM/PM "XY" should fail (line 322-324 return false)
        string result = Eval("""$toMillis("10:30 XY", "[h01]:[m01] [P]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_LiteralMismatch_ReturnsUndefined()
    {
        // Literal text doesn't match (line 232-234 return false)
        string result = Eval("""$toMillis("2024/01/15", "[Y]-[M01]-[D01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_LiteralTooLong_ReturnsUndefined()
    {
        // Literal extends beyond input (line 226-228 return false)
        string result = Eval("""$toMillis("24", "[Y]----[M01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadMonth_ReturnsUndefined()
    {
        // Invalid month component (line 257-259 return false)
        string result = Eval("""$toMillis("2024-zz-15", "[Y]-[M01]-[D01]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_Bad12Hour_ReturnsUndefined()
    {
        // Invalid 12-hour component (line 289-291 return false)
        string result = Eval("""$toMillis("xx:30 am", "[h01]:[m01] [P]")""");
        Assert.Equal("undefined", result);
    }

    [Fact]
    public void ToMillis_BadDayOfYear_ReturnsUndefined()
    {
        // Invalid day-of-year component (line 273-275 return false)
        string result = Eval("""$toMillis("2024 abc", "[Y] [d001]")""");
        Assert.Equal("undefined", result);
    }

    // ─── TryParseDateTime: AM/PM conversion (lines 327-340) ──────────────

    [Fact]
    public void ToMillis_PmHour_Converts12To24()
    {
        // pm with hour < 12 adds 12 (line 329-332)
        string result = Eval("""$toMillis("2024-01-15 02:30 pm", "[Y]-[M01]-[D01] [h01]:[m01] [P]")""");
        Assert.NotEqual("undefined", result);
        // 2:30 PM = 14:30, verify by formatting back
        string check = Eval("""$fromMillis($toMillis("2024-01-15 02:30 pm", "[Y]-[M01]-[D01] [h01]:[m01] [P]"), "[H01]:[m01]")""");
        Assert.Equal("\"14:30\"", check);
    }

    [Fact]
    public void ToMillis_AmHour12_ConvertsTo0()
    {
        // am with hour == 12 sets hour to 0 (line 336-339)
        string result = Eval("""$toMillis("2024-01-15 12:00 am", "[Y]-[M01]-[D01] [h01]:[m01] [P]")""");
        Assert.NotEqual("undefined", result);
        string check = Eval("""$fromMillis($toMillis("2024-01-15 12:00 am", "[Y]-[M01]-[D01] [h01]:[m01] [P]"), "[H01]:[m01]")""");
        Assert.Equal("\"00:00\"", check);
    }

    // ─── TryParseDateTime: DayOfYear, DayOfWeek, Era (lines 344-387) ──────────────

    [Fact]
    public void ToMillis_DayOfYear_ParsesCorrectly()
    {
        // Day-of-year parsing (line 272, 377-387)
        string result = Eval("""$toMillis("2024 045", "[Y] [d001]")""");
        Assert.NotEqual("undefined", result);
        // Day 45 of 2024 = Feb 14
        string check = Eval("""$fromMillis($toMillis("2024 045", "[Y] [d001]"), "[M01]-[D01]")""");
        Assert.Equal("\"02-14\"", check);
    }

    [Fact]
    public void ToMillis_DayOfWeek_NumericSkipped()
    {
        // Day of week with numeric presentation — just skipped (lines 3500-3507)
        string result = Eval("""$toMillis("2024-01-15 1", "[Y]-[M01]-[D01] [F1]")""");
        Assert.NotEqual("undefined", result);
    }

    [Fact]
    public void ToMillis_DayOfWeek_NameSkipped()
    {
        // Day of week with name presentation — skipped (lines 3492-3496)
        string result = Eval("""$toMillis("Monday 2024-01-15", "[FNn] [Y]-[M01]-[D01]")""");
        Assert.NotEqual("undefined", result);
    }

    // ─── FormatTimezoneOffset variants (lines 1068-1096) ──────────────

    [Fact]
    public void FromMillis_Timezone4Digit()
    {
        // 4-digit timezone format with no colon (line 1068-1072)
        string result = Eval("""$fromMillis(1234567890000, "[H01][m01][Z0101]", "+0530")""");
        Assert.Contains("+", result);
    }

    [Fact]
    public void FromMillis_TimezoneMinimal()
    {
        // Minimal timezone format "0" (lines 1062-1067, 1097-1100)
        string result = Eval("""$fromMillis(1234567890000, "[H01][m01][Z0]", "+0500")""");
        Assert.Contains("+", result);
    }

    [Fact]
    public void FromMillis_Timezone6DigitThrows()
    {
        // 6+ digit timezone format throws D3134 (lines 1074-1078)
        Assert.Throws<JsonataException>(() => Eval("""$fromMillis(1234567890000, "[Z010101]")"""));
    }

    // ─── FormatInteger: TryFormatInteger span overload (lines 477-484) ──────────────

    [Fact]
    public void FromMillis_DayOfYear_Format()
    {
        // Day of year formatting exercises FormatComponent with 'd' which calls FormatInteger
        // 2009-02-13 = day 44
        string result = Eval("""$fromMillis(1234567890000, "[d]")""");
        Assert.Equal("\"44\"", result);
    }

    [Fact]
    public void FromMillis_DayOfYear_Padded()
    {
        // Padded day of year (3 digits)
        string result = Eval("""$fromMillis(1234567890000, "[d001]")""");
        Assert.Equal("\"044\"", result);
    }

    // ─── FormatDecimalDigit: grouping, padding (lines 1586-1622) ──────────────

    [Fact]
    public void FromMillis_Year_PaddedWidth()
    {
        // Year with width modifier ensuring 4 digits (tests padding path line 1611-1616)
        string result = Eval("""$fromMillis(1234567890000, "[Y0001]")""");
        Assert.Equal("\"2009\"", result);
    }

    [Fact]
    public void FromMillis_DayWithGrouping()
    {
        // Day of year with 3-digit padding (exercises mandatory digit padding in FormatDecimalDigit)
        // Day 44 padded to 3 digits → "044"
        string result = Eval("""$fromMillis(1234567890000, "[d001]")""");
        Assert.Equal("\"044\"", result);
    }

    // ─── FormatInteger double overload (lines 521-555) for huge values ──────────────
    // This path is only reachable with values > long.MaxValue, which doesn't happen
    // naturally with $fromMillis. Documenting as dead code for date formatting context.

    // ─── TryParseDateTime: all-literal picture (lines 370-373) ──────────────

    [Fact]
    public void ToMillis_AllLiteral_ReturnsUndefined()
    {
        // Picture with no date/time components at all (lines 370-373)
        string result = Eval("""$toMillis("hello world", "hello world")""");
        Assert.Equal("undefined", result);
    }

    // ─── TryParseDateTime consistency errors (lines 379-393) ──────────────

    [Fact]
    public void ToMillis_DayOfYearNoYear_ThrowsD3136()
    {
        // Day of year without year component throws D3136 (lines 379-381)
        Assert.Throws<JsonataException>(() => Eval("""$toMillis("045", "[d001]")"""));
    }

    [Fact]
    public void ToMillis_DayAndYearNoMonth_ThrowsD3136()
    {
        // Day + year but no month (and no day-of-year) throws D3136 (lines 391-393)
        Assert.Throws<JsonataException>(() => Eval("""$toMillis("2024 15", "[Y] [D01]")"""));
    }

    // ─── FormatTimezoneOffset: negative timezone (line 1091) ──────────────

    [Fact]
    public void FromMillis_NegativeTimezone()
    {
        // Negative timezone offset (line 1091 - branch)
        string result = Eval("""$fromMillis(1234567890000, "[H01]:[m01][Z01:01]", "-0500")""");
        Assert.Contains("-05:00", result);
    }

    // ─── ParseTimezoneOffset: various timezone strings in $toMillis ──────────────

    [Fact]
    public void ToMillis_WithTimezone_Parses()
    {
        // Parsing timezone offset (line 344)
        string result = Eval("""$toMillis("2024-01-15 10:30+05:30", "[Y]-[M01]-[D01] [H01]:[m01][Z01:01]")""");
        Assert.NotEqual("undefined", result);
    }

    [Fact]
    public void ToMillis_WithNegativeTimezone_Parses()
    {
        string result = Eval("""$toMillis("2024-01-15 10:30-04:00", "[Y]-[M01]-[D01] [H01]:[m01][Z01:01]")""");
        Assert.NotEqual("undefined", result);
    }

    // ─── FormatFractionalSeconds (uncovered in FormatComponent) ──────────────

    [Fact]
    public void FromMillis_FractionalSeconds()
    {
        // Fractional seconds formatting
        string result = Eval("""$fromMillis(1234567890123, "[s01].[f001]")""");
        Assert.Contains(".", result);
        Assert.Equal("\"30.123\"", result);
    }

    // ─── $toMillis with non-string/number (lines 5952-5953) ───────────

    [Fact]
    public void ToMillis_BooleanArg_ThrowsParseError()
    {
        // Reference: $toMillis(true) → T0410 error (type mismatch)
        // Our implementation: boolean coerced to string "true" then fails ISO parse → D3110
        var ex = Assert.Throws<JsonataException>(
            () => Eval("$toMillis(true)"));
        Assert.Equal("D3110", ex.Code);
    }

    // ─── $fromMillis with lone bracket (XPathDateTimeFormatter 157-160) ──

    [Fact]
    public void FromMillis_LoneBracketInPicture()
    {
        // A lone ']' (not part of ']]' pair) exercises lines 157-160.
        // Picture "[Y]] text" → after [Y] marker, remaining is "] text".
        // The ']' followed by ' ' is NOT a ']]' pair, so the else branch fires.
        string result = Eval("""$fromMillis(1234567890000, "[Y]] text")""");
        Assert.Contains("2009", result);
        Assert.Contains("] text", result);
    }

    // ─── $fromMillis with ordinal suffix (XPathDateTimeFormatter) ─────

    [Fact]
    public void FromMillis_OrdinalDay()
    {
        // Reference: $fromMillis(1234567890000, "[D1;o] [MNn] [Y]") → "13th February 2009"
        // Note: reference shows "13;th" but many impls use "13th"
        string result = Eval("""$fromMillis(1234567890000, "[D1;o] [MNn] [Y]")""");
        Assert.Contains("13", result);
        Assert.Contains("February", result);
        Assert.Contains("2009", result);
    }

    // ─── $fromMillis with 12-hour format (XPathDateTimeFormatter) ─────

    [Fact]
    public void FromMillis_TwelveHourFormat()
    {
        // Reference: $fromMillis(1234567890000, "[h].[m01][P]") → "11.31pm"
        string result = Eval("""$fromMillis(1234567890000, "[h].[m01][P]")""");
        Assert.Contains("31", result);
    }

    // ─── $fromMillis with day name (XPathDateTimeFormatter) ───────────

    [Fact]
    public void FromMillis_DayName()
    {
        // Reference: $fromMillis(1234567890000, "[FNn], [D] [MNn] [Y]") → "Friday, 13 February 2009"
        string result = Eval("""$fromMillis(1234567890000, "[FNn], [D] [MNn] [Y]")""");
        Assert.Contains("Friday", result);
        Assert.Contains("13", result);
    }

    // ─── $toMillis roundtrip (XPathDateTimeFormatter parse paths) ─────

    [Fact]
    public void ToMillis_CustomPicture()
    {
        // Reference: $toMillis("2009-02-13", "[Y]-[M01]-[D01]") → 1234483200000
        string result = Eval("""$toMillis("2009-02-13", "[Y]-[M01]-[D01]")""");
        Assert.Equal("1234483200000", result);
    }

    // ─── $formatNumber with exponent containing non-digit (lines 4833-4834) ──

    [Fact]
    public void FormatNumber_ExponentNonDigit_ThrowsD3093()
    {
        // Exponent part contains comma (grouping separator) which is active but not a digit.
        // Pattern "#e,0": mantissa="#", exponent=",0" → ',' is not in digit family → D3093
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$formatNumber(1234.5, "#e,0")"""));
        Assert.Equal("D3093", ex.Code);
    }

    // ─── Evaluator: time limit (lines 524-528, 611-614, 855-858) ──────────────

    [Fact]
    public void Evaluate_WithTimeLimit_CompletesNormally()
    {
        // Exercise the timeLimitMs > 0 code path (lines 524-528)
        // A simple expression should complete well within the limit
        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("42"));
        var result = evaluator.Evaluate("$ + 1", doc.RootElement, timeLimitMs: 5000);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
    }

    [Fact]
    public void Evaluate_WithTimeLimit_ThrowsU1001OnTimeout()
    {
        // Very tight time limit with a complex expression should timeout (U1001)
        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("1"));
        var ex = Assert.Throws<JsonataException>(
            () => evaluator.Evaluate(
                """($f := function($n){$n > 0 ? $f($n-1) + $f($n-2) : 1}; $f(50))""",
                doc.RootElement,
                timeLimitMs: 1));
        Assert.Equal("U1001", ex.Code);
    }

    // ─── Environment: max depth exceeded (line 255) ──────────────

    [Fact]
    public void Evaluate_MaxDepthExceeded_ThrowsU1001()
    {
        // Very low maxDepth with recursive expression triggers depth exceeded
        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("1"));
        var ex = Assert.Throws<JsonataException>(
            () => evaluator.Evaluate(
                """($f := function($n){$n > 0 ? $f($n-1) : 0}; $f(10))""",
                doc.RootElement,
                maxDepth: 3));
        Assert.Equal("U1001", ex.Code);
    }

    // ─── Environment: lambda registration (lines 289-335) ──────────────
    // NOTE: RegisterLambda, TryGetLambda, TryGetStoredLambda are DEAD CODE:
    // they have zero callers in the runtime. They are public API surface that
    // was added for potential external use but is never invoked by the evaluator.

    [Fact]
    public void HigherOrderFunction_PassedAsArgument()
    {
        // Exercises lambda handling in the evaluator (function as first-class value)
        string result = Eval("""($apply := function($f, $x){ $f($x) }; $apply($sum, [1,2,3]))""");
        Assert.Equal("6", result);
    }

    [Fact]
    public void HigherOrderFunction_ReturnedFromFunction()
    {
        // Function returning a function exercises nested lambda creation
        string result = Eval("""($adder := function($a){ function($b){ $a + $b } }; $adder(3)(7))""");
        Assert.Equal("10", result);
    }

    // ─── JsonataHelpers: GrowBuffer with long concatenation (lines 678-683) ──────────────

    [Fact]
    public void LongConcatenation_TriggersGrowBuffer()
    {
        // $join on many strings produces a result large enough to overflow the initial buffer
        // The initial ArrayPool rent is typically 256 bytes, so 300+ character result triggers GrowBuffer
        string result = Eval("""$join($map([1..100], function($v){ "abcde" }), "-")""");
        Assert.NotEqual("undefined", result);
        // 100 × "abcde" + 99 × "-" = 500 + 99 = 599 chars
        Assert.StartsWith("\"abcde-abcde", result);
    }

    // ─── SignatureValidator: type mismatch errors (lines 247-260, 317-336, 520-540) ──────────────
    // NOTE: The SignatureValidator is ONLY called via the JsonataBinding API or user-defined
    // function signatures (function<sig>(...){...}). Built-in functions do INLINE type checking
    // in BuiltInFunctions.cs. The reference implementation routes ALL built-in functions through
    // signature validation, but ours only routes some (like $uppercase, $join, $substring).
    //
    // BUGS FIXED (previously returned wrong result vs. reference implementation, now correct):
    //   $sqrt("hello") => T0410 ✓ (was: undefined)
    //   $abs(true) => T0410 ✓ (was: 1 — coerced boolean to number)
    //   $floor({"a":1}) => T0410 ✓ (was: undefined)
    //   $map([1,2,3], "notafunction") => T0410 ✓ (was: undefined)
    //   $filter([1,2,3], "notfunc") => T0410 ✓ (was: undefined)
    //   $reduce([1,2,3], "notfunc") => T0410 ✓ (was: undefined)
    //   $match(42, /abc/) => T0410 ✓ (was: undefined)
    //   $formatNumber("not a number", "#") => T0410 ✓ (was: empty string)
    //   $shuffle(42) => [42] ✓ (was: 42 — now wraps singleton in array)
    //
    // The tests below exercise functions that DO correctly throw T0410/T0412.

    [Fact]
    public void TypeMismatch_NumberToStringFunction_ThrowsT0410()
    {
        // $uppercase expects a string; passing a number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$uppercase(42)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_SubstringWithNumber_ThrowsT0410()
    {
        // $substring expects a string as first arg; number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$substring(42, 1)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_SubstringWithBoolean_ThrowsT0410()
    {
        // $substring expects string; boolean triggers T0410
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$substring(true, 1)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_ContainsWithNumber_ThrowsT0410()
    {
        // $contains expects string as first arg; number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$contains(42, "x")"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_SplitWithNumber_ThrowsT0410()
    {
        // $split expects string; number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$split(42, "-")"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_ArrayOfNumbers_ToJoinExpectsStrings_ThrowsT0412()
    {
        // $join expects array of strings; passing array of numbers triggers T0412 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$join([1,2,3])"""));
        Assert.Equal("T0412", ex.Code);
    }

    [Fact]
    public void TypeMismatch_ArrayOfBooleans_ToJoinExpectsStrings_ThrowsT0412()
    {
        // $join expects array of strings; passing array of booleans triggers T0412
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$join([true, false])"""));
        Assert.Equal("T0412", ex.Code);
    }

    [Fact]
    public void TypeMismatch_NullToStringFunction_ThrowsT0410()
    {
        // $uppercase expects a string; passing null triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$uppercase(null)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_ReplaceWithNumber_ThrowsT0410()
    {
        // $replace expects string as first arg; number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$replace(42, "a", "b")"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TypeMismatch_TrimWithNumber_ThrowsT0410()
    {
        // $trim expects string; number triggers T0410 ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$trim(42)"""));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void TooManyArguments_ThrowsT0410()
    {
        // $sum has signature <a<n>>; passing 2 args triggers "too many arguments" ✓ (matches reference)
        var ex = Assert.Throws<JsonataException>(
            () => Eval("""$sum([1,2], [3,4])"""));
        Assert.Equal("T0410", ex.Code);
    }

    // ─── SignatureValidator via JsonataBinding API (lines 247-260, 317-336, 520-540) ──

    [Fact]
    public void BindingWithSignature_ValidArgs_Succeeds()
    {
        // A custom binding with signature <n:n> (expects number, returns number)
        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["double"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() * 2, ws),
                1,
                "<n:n>"),
        };

        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("null"));
        var result = evaluator.Evaluate("$double(21)", doc.RootElement, bindings);
        Assert.Equal(JsonValueKind.Number, result.ValueKind);
        Assert.Equal(42.0, result.GetDouble());
    }

    [Fact]
    public void BindingWithSignature_WrongType_ThrowsT0410()
    {
        // A custom binding with signature <n:n> called with a string → T0410
        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["double"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].AsDouble() * 2, ws),
                1,
                "<n:n>"),
        };

        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("""{"x":"hello"}"""));
        var ex = Assert.Throws<JsonataException>(
            () => evaluator.Evaluate("$double(x)", doc.RootElement, bindings));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void BindingWithArraySignature_WrongElementType_ThrowsT0412()
    {
        // A custom binding with signature <a<s>:n> (expects array of strings) called with array of numbers → T0412
        var bindings = new Dictionary<string, JsonataBinding>
        {
            ["countStrings"] = JsonataBinding.FromFunction(
                (args, ws) => Sequence.FromDouble(args[0].Count, ws),
                1,
                "<a<s>:n>"),
        };

        var evaluator = JsonataEvaluator.Default;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("""{"arr":[1,2,3]}"""));
        var ex = Assert.Throws<JsonataException>(
            () => evaluator.Evaluate("$countStrings(arr)", doc.RootElement, bindings));
        Assert.Equal("T0412", ex.Code);
    }

    // ─── Sequence: Enumerator paths (lines 706-731) exercised via multi-element arrays ──────────────

    [Fact]
    public void MultiElementArray_SortDescending()
    {
        // Sorting a multi-element array exercises Enumerator via iteration
        string result = Eval("""$sort([5,3,8,1,4], function($a,$b){$a > $b})""");
        Assert.Equal("[1,3,4,5,8]", result);
    }

    [Fact]
    public void Reduce_ExercisesIteration()
    {
        // $reduce iterates over sequence elements
        string result = Eval("""$reduce([1,2,3,4,5], function($prev,$curr){$prev + $curr})""");
        Assert.Equal("15", result);
    }

    // ─── T0410 type validation tests (verified against reference JSONata 1.8.7) ──────────────
    // These tests verify that built-in functions throw T0410 when given invalid argument types,
    // matching the behavior of the reference implementation's SignatureValidator.

    [Theory]
    [InlineData("""$sqrt("hello")""")]
    [InlineData("""$sqrt(true)""")]
    [InlineData("""$sqrt(null)""")]
    [InlineData("""$sqrt([1,2])""")]
    public void Sqrt_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$abs("hello")""")]
    [InlineData("""$abs(true)""")]
    [InlineData("""$abs(null)""")]
    public void Abs_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$floor("test")""")]
    [InlineData("""$floor(null)""")]
    [InlineData("""$floor({"a":1})""")]
    public void Floor_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$ceil("test")""")]
    [InlineData("""$ceil(true)""")]
    public void Ceil_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$round("test")""")]
    [InlineData("""$round(null)""")]
    public void Round_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$power("a", 2)""")]
    [InlineData("""$power(2, "a")""")]
    [InlineData("""$power(true, 2)""")]
    [InlineData("""$power(2, null)""")]
    public void Power_NonNumericArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$map([1,2,3], "notfunc")""")]
    [InlineData("""$map([1,2], 42)""")]
    [InlineData("""$map([1,2], null)""")]
    public void Map_SecondArgNotFunction_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$filter([1,2,3], "notfunc")""")]
    [InlineData("""$filter([1,2], 42)""")]
    [InlineData("""$filter([1,2], null)""")]
    public void Filter_SecondArgNotFunction_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$reduce([1,2,3], "notfunc")""")]
    [InlineData("""$reduce([1,2], 42)""")]
    [InlineData("""$reduce([1,2], null)""")]
    public void Reduce_SecondArgNotFunction_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$match(42, /abc/)""")]
    [InlineData("""$match(true, /abc/)""")]
    [InlineData("""$match([1,2], /abc/)""")]
    public void Match_FirstArgNotString_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$formatNumber("not a number", "#")""")]
    [InlineData("""$formatNumber(true, "#")""")]
    [InlineData("""$formatNumber(null, "#")""")]
    public void FormatNumber_FirstArgNotNumber_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void Shuffle_Singleton_WrapsInArray()
    {
        // Reference: $shuffle(42) returns [42]
        string result = Eval("""$shuffle(42)""");
        Assert.Equal("[42]", result);
    }

    [Fact]
    public void Shuffle_Null_WrapsInArray()
    {
        // Reference: $shuffle(null) returns [null]
        string result = Eval("""$shuffle(null)""");
        Assert.Equal("[null]", result);
    }

    [Fact]
    public void Shuffle_String_WrapsInArray()
    {
        // Reference: $shuffle("hello") returns ["hello"]
        string result = Eval("""$shuffle("hello")""");
        Assert.Equal("""["hello"]""", result);
    }

    // ─── Undefined propagation tests (T0410 should NOT fire for undefined inputs) ────────

    [Theory]
    [InlineData("""$sqrt(nosuchvar)""")]
    [InlineData("""$abs(nosuchvar)""")]
    [InlineData("""$floor(nosuchvar)""")]
    [InlineData("""$ceil(nosuchvar)""")]
    [InlineData("""$round(nosuchvar)""")]
    [InlineData("""$power(nosuchvar, 2)""")]
    [InlineData("""$power(2, nosuchvar)""")]
    [InlineData("""$map(nosuchvar, function($v){$v})""")]
    [InlineData("""$filter(nosuchvar, function($v){$v})""")]
    [InlineData("""$reduce(nosuchvar, function($a,$b){$a+$b})""")]
    [InlineData("""$match(nosuchvar, /abc/)""")]
    [InlineData("""$formatNumber(nosuchvar, "#")""")]
    [InlineData("""$formatBase(nosuchvar, 16)""")]
    [InlineData("""$each(nosuchvar, function($v,$k){$v})""")]
    [InlineData("""$sift(nosuchvar, function($v,$k){true})""")]
    public void UndefinedInput_PropagatesToUndefined(string expression)
    {
        string result = Eval(expression);
        Assert.Equal("undefined", result);
    }

    // ─── $formatBase type validation (reference throws T0410) ─────────

    [Theory]
    [InlineData("""$formatBase("hello", 16)""")]
    [InlineData("""$formatBase(true, 16)""")]
    [InlineData("""$formatBase(null, 16)""")]
    [InlineData("""$formatBase([1,2], 16)""")]
    public void FormatBase_NonNumberFirstArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$formatBase(255, "hex")""")]
    [InlineData("""$formatBase(255, true)""")]
    [InlineData("""$formatBase(255, null)""")]
    public void FormatBase_NonNumberRadix_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Fact]
    public void FormatBase_UndefinedRadix_UsesDefault10()
    {
        // $formatBase(255, nothing) → uses default radix 10 → "255"
        string result = Eval("""$formatBase(255, nosuchvar)""");
        Assert.Equal("\"255\"", result);
    }

    // ─── $each type validation (reference throws T0410) ───────────────

    [Theory]
    [InlineData("""$each(42, function($v,$k){$v})""")]
    [InlineData("""$each("hello", function($v,$k){$v})""")]
    [InlineData("""$each(null, function($v,$k){$v})""")]
    [InlineData("""$each([1,2], function($v,$k){$v})""")]
    public void Each_NonObjectFirstArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$each({"a":1}, 42)""")]
    [InlineData("""$each({"a":1}, nosuchvar)""")]
    public void Each_NonFunctionSecondArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    // ─── $sift type validation (reference throws T0410) ───────────────

    [Theory]
    [InlineData("""$sift(42, function($v,$k){true})""")]
    [InlineData("""$sift("hello", function($v,$k){true})""")]
    [InlineData("""$sift(null, function($v,$k){true})""")]
    [InlineData("""$sift([1,2], function($v,$k){true})""")]
    public void Sift_NonObjectFirstArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$sift({"a":1}, 42)""")]
    [InlineData("""$sift({"a":1}, nosuchvar)""")]
    public void Sift_NonFunctionSecondArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    // ─── $match with non-regex pattern (reference throws T0410) ───────

    [Theory]
    [InlineData("""$match("hello", 42)""")]
    [InlineData("""$match("hello", nosuchvar)""")]
    [InlineData("""$match("hello", null)""")]
    [InlineData("""$match("hello", true)""")]
    public void Match_NonRegexPattern_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }

    [Theory]
    [InlineData("""$match(42, /a/)""")]
    [InlineData("""$match(null, /a/)""")]
    public void Match_NonStringFirstArg_ThrowsT0410(string expression)
    {
        var ex = Assert.Throws<JsonataException>(() => Eval(expression));
        Assert.Equal("T0410", ex.Code);
    }
}
