// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 union type (oneOf) checking produces equivalent results.
/// </summary>
/// <remarks>
/// <para>
/// Both V4 and V5 emit <c>TryGetAs*()</c> methods for union variants. The type used in the method
/// name comes from whatever type the variant resolved to. V4 reduced unformatted simple types to
/// framework globals from <c>Corvus.Json.ExtendedTypes</c> (e.g. <c>JsonString</c>,
/// <c>JsonBoolean</c>, <c>JsonInteger</c>) and <c>"type":"number"</c> with a numeric format to
/// globals (e.g. <c>JsonInt32</c>, <c>JsonDouble</c>). However, V4 did <em>not</em> reduce
/// <c>"type":"integer"</c> with a format — those became custom entity types like
/// <c>OneOf1Entity</c>. V4 also had no equivalent for V5's <c>Json&lt;Format&gt;NotAsserted</c>
/// globals. V5 reduces all of these cases to project-local global types.
/// </para>
/// <para>
/// The other difference is in how multi-core-type types handle value accessors. In V4, when a type
/// composed multiple core types (e.g. a union of string and boolean), V4 would <em>not</em> emit
/// value accessors (casts, <c>GetString()</c>, indexers, etc.) directly on the union type. You had
/// to go through <c>AsString</c>, <c>AsBoolean</c>, etc. to reach a single-core-type that did have
/// those accessors. In V5, value accessors are emitted for all composed core types directly on the
/// type, so you can use <c>(string)v5</c>, <c>(int)v5</c>, <c>(bool)v5</c>, <c>TryGetValue()</c>,
/// etc. without the <c>As*</c> indirection.
/// </para>
/// </remarks>
public class UnionEquivalenceTests
{
    [Fact]
    public void V4_StringVariant_ValueKindAndExtract()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
        Assert.Equal("hello", (string)v4);
    }

    [Fact]
    public void V4_StringVariant_ValueKindAndExtract_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.String, v4.ValueKind);
        Assert.Equal("hello", (string)v4);
    }

    [Fact]
    public void V5_StringVariant_ValueKindAndExtract()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5.ValueKind);
        Assert.Equal("hello", (string)v5);
    }

    [Fact]
    public void V4_IntVariant_ValueKindAndExtract()
    {
        var v4 = V4.MigrationUnion.Parse("""42""");
        Assert.Equal(System.Text.Json.JsonValueKind.Number, v4.ValueKind);
        Assert.Equal(42, (int)v4);
    }

    [Fact]
    public void V4_IntVariant_ValueKindAndExtract_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("""42""");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Number, v4.ValueKind);
        Assert.Equal(42, (int)v4);
    }

    [Fact]
    public void V5_IntVariant_ValueKindAndExtract()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("""42""");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Number, v5.ValueKind);
        Assert.Equal(42, (long)v5);
    }

    [Fact]
    public void V4_BoolVariant_True()
    {
        var v4 = V4.MigrationUnion.Parse("""true""");
        Assert.Equal(System.Text.Json.JsonValueKind.True, v4.ValueKind);
        Assert.True((bool)v4);
    }

    [Fact]
    public void V4_BoolVariant_True_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("""true""");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.True, v4.ValueKind);
        Assert.True((bool)v4);
    }

    [Fact]
    public void V5_BoolVariant_True()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("""true""");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.True, v5.ValueKind);
        Assert.True((bool)v5);
    }

    [Fact]
    public void V4_BoolVariant_False()
    {
        var v4 = V4.MigrationUnion.Parse("""false""");
        Assert.Equal(System.Text.Json.JsonValueKind.False, v4.ValueKind);
        Assert.False((bool)v4);
    }

    [Fact]
    public void V4_BoolVariant_False_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("""false""");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.False, v4.ValueKind);
        Assert.False((bool)v4);
    }

    [Fact]
    public void V5_BoolVariant_False()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("""false""");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.False, v5.ValueKind);
        Assert.False((bool)v5);
    }

    [Fact]
    public void V4_AsStringAccessor()
    {
        // V4: Multi-core-type types didn't emit value accessors directly.
        // AsString returns a single-core-type (JsonString) that does have them.
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Corvus.Json.JsonString asString = v4.AsString;
        Assert.Equal("hello", (string)asString);
    }

    [Fact]
    public void V4_AsStringAccessor_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonString asString = v4.AsString;
        Assert.Equal("hello", (string)asString);
    }

    [Fact]
    public void V5_DirectValueAccess_String_EquivalentToAsString()
    {
        // V5: Value accessors are emitted for all composed core types directly on the type,
        // so you no longer need AsString — just use the cast, GetString(), or TryGetValue().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal("hello", (string)v5);
    }

    [Fact]
    public void V4_AsNumberAccessor()
    {
        // V4: Multi-core-type types didn't emit value accessors directly.
        // AsNumber returns a single-core-type (JsonNumber) that does have them.
        var v4 = V4.MigrationUnion.Parse("42");
        Corvus.Json.JsonNumber asNumber = v4.AsNumber;
        Assert.Equal(42, (int)asNumber);
    }

    [Fact]
    public void V4_AsNumberAccessor_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonNumber asNumber = v4.AsNumber;
        Assert.Equal(42, (int)asNumber);
    }

    [Fact]
    public void V5_DirectValueAccess_Number_EquivalentToAsNumber()
    {
        // V5: Value accessors are emitted for all composed core types directly on the type,
        // so you no longer need AsNumber — just use the cast or TryGetValue().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(42, (int)v5);
    }

    [Fact]
    public void V4_AsBooleanAccessor()
    {
        // V4: Multi-core-type types didn't emit value accessors directly.
        // AsBoolean returns a single-core-type (JsonBoolean) that does have them.
        var v4 = V4.MigrationUnion.Parse("true");
        Corvus.Json.JsonBoolean asBoolean = v4.AsBoolean;
        Assert.True((bool)asBoolean);
    }

    [Fact]
    public void V4_AsBooleanAccessor_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonBoolean asBoolean = v4.AsBoolean;
        Assert.True((bool)asBoolean);
    }

    [Fact]
    public void V5_DirectValueAccess_Boolean_EquivalentToAsBoolean()
    {
        // V5: Value accessors are emitted for all composed core types directly on the type,
        // so you no longer need AsBoolean — just use the cast or TryGetValue().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True((bool)v5);
    }

    [Fact]
    public void BothEngines_TryGetAs_String_SamePattern()
    {
        // TryGetAs*() is emitted for any variant type — local or global — in both V4 and V5.
        // V4: variant resolved to framework built-in Corvus.Json.JsonString
        // V5: variant resolved to project-local global simple type V5.JsonString
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsJsonString(out Corvus.Json.JsonString v4Result));
        Assert.Equal("hello", (string)v4Result);

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonString(out V5.JsonString v5Result));
        Assert.Equal("hello", (string)v5Result);

        Assert.Equal((string)v4Result, (string)v5Result);
    }

    [Fact]
    public void BothEngines_TryGetAs_Number_DifferentNames()
    {
        // TryGetAs*() uses whatever type the variant resolved to.
        // V4: {"type":"integer","format":"int32"} is NOT reduced to a framework global
        //     (V4 only reduces "type":"number" + format, not "type":"integer" + format)
        //     so the variant becomes a custom OneOf1Entity.
        // V5: reduces to project-local global simple type JsonInt32
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsOneOf1Entity(out V4.MigrationUnion.OneOf1Entity v4Result));
        Assert.Equal(42, (int)v4Result);

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonInt32(out V5.JsonInt32 v5Result));
        Assert.Equal(42, (int)v5Result);

        Assert.Equal((int)v4Result, (int)v5Result);
    }

    [Fact]
    public void BothEngines_TryGetAs_Boolean_SamePattern()
    {
        // TryGetAs*() is emitted for any variant type — local or global — in both V4 and V5.
        // V4: variant resolved to framework built-in Corvus.Json.JsonBoolean
        // V5: variant resolved to project-local global simple type V5.JsonBoolean
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsJsonBoolean(out Corvus.Json.JsonBoolean v4Result));
        Assert.True((bool)v4Result);

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonBoolean(out V5.JsonBoolean v5Result));
        Assert.True((bool)v5Result);

        Assert.Equal((bool)v4Result, (bool)v5Result);
    }

    [Fact]
    public void V5_MatchPattern_String()
    {
        // V5: Match<TResult> discriminated union pattern
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("string:hello", result);
    }

    [Fact]
    public void V5_MatchPattern_Number()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("number:42", result);
    }

    [Fact]
    public void V5_MatchPattern_Boolean()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("bool:True", result);
    }

    [Fact]
    public void V4_MatchPattern_String()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("string:hello", result);
    }

    [Fact]
    public void V4_MatchPattern_Number()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("number:42", result);
    }

    [Fact]
    public void V4_MatchPattern_Boolean()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            static (in s) => $"string:{(string)s}",
            static (in n) => $"number:{(int)n}",
            static (in b) => $"bool:{(bool)b}",
            static (in v) => "none");
        Assert.Equal("bool:True", result);
    }

    [Fact]
    public void BothEngines_MatchWithoutContext_SameResult()
    {
        string[] jsons = ["\"hello\"", "42", "true"];
        string[] expected = ["string:hello", "number:42", "bool:True"];

        for (int i = 0; i < jsons.Length; i++)
        {
            using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse(jsons[i]);
            V4.MigrationUnion v4 = parsedV4.Instance;
            string v4Result = v4.Match(
                static (in s) => $"string:{(string)s}",
                static (in n) => $"number:{(int)n}",
                static (in b) => $"bool:{(bool)b}",
                static (in v) => "none");

            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse(jsons[i]);
            V5.MigrationUnion v5 = parsedV5.RootElement;
            string v5Result = v5.Match(
                static (in s) => $"string:{(string)s}",
                static (in n) => $"number:{(int)n}",
                static (in b) => $"bool:{(bool)b}",
                static (in v) => "none");

            Assert.Equal(expected[i], v4Result);
            Assert.Equal(v4Result, v5Result);
        }
    }

    [Fact]
    public void V4_MatchPatternWithContext_String()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:string:hello", result);
    }

    [Fact]
    public void V4_MatchPatternWithContext_Number()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:number:42", result);
    }

    [Fact]
    public void V4_MatchPatternWithContext_Boolean()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:bool:True", result);
    }

    [Fact]
    public void V5_MatchPatternWithContext_String()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:string:hello", result);
    }

    [Fact]
    public void V5_MatchPatternWithContext_Number()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:number:42", result);
    }

    [Fact]
    public void V5_MatchPatternWithContext_Boolean()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            "prefix",
            static (in s, in ctx) => $"{ctx}:string:{(string)s}",
            static (in n, in ctx) => $"{ctx}:number:{(int)n}",
            static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
            static (in v, in ctx) => $"{ctx}:none");
        Assert.Equal("prefix:bool:True", result);
    }

    [Fact]
    public void BothEngines_MatchWithContext_SameResult()
    {
        // Both V4 and V5 Match<TContext, TResult> produce the same output for each variant
        string[] jsons = ["\"hello\"", "42", "true"];
        string[] expected = ["prefix:string:hello", "prefix:number:42", "prefix:bool:True"];

        for (int i = 0; i < jsons.Length; i++)
        {
            using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse(jsons[i]);
            V4.MigrationUnion v4 = parsedV4.Instance;
            string v4Result = v4.Match(
                "prefix",
                static (in s, in ctx) => $"{ctx}:string:{(string)s}",
                static (in n, in ctx) => $"{ctx}:number:{(int)n}",
                static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
                static (in v, in ctx) => $"{ctx}:none");

            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse(jsons[i]);
            V5.MigrationUnion v5 = parsedV5.RootElement;
            string v5Result = v5.Match(
                "prefix",
                static (in s, in ctx) => $"{ctx}:string:{(string)s}",
                static (in n, in ctx) => $"{ctx}:number:{(int)n}",
                static (in b, in ctx) => $"{ctx}:bool:{(bool)b}",
                static (in v, in ctx) => $"{ctx}:none");

            Assert.Equal(expected[i], v4Result);
            Assert.Equal(v4Result, v5Result);
        }
    }

    [Fact]
    public void V4_UnionValidation_StringValid()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V4_UnionValidation_StringValid_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.ValidationContext result = v4.Validate(Corvus.Json.ValidationContext.ValidContext, Corvus.Json.ValidationLevel.Flag);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void V5_UnionValidation_StringValid()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.EvaluateSchema());
    }

    [Fact]
    public void BothEngines_StringVariant_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;

        Assert.Equal((string)v4, (string)v5);
    }

    [Fact]
    public void BothEngines_IntVariant_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;

        Assert.Equal((long)v4, (long)v5);
    }

    [Fact]
    public void BothEngines_BoolVariant_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;

        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;

        Assert.Equal((bool)v4, (bool)v5);
    }

    [Fact]
    public void V5_ExplicitCast_DoubleFromNumberVariant()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("3.14");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(3.14, (double)v5);
    }

    [Fact]
    public void V5_ExplicitCast_DecimalFromNumberVariant()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("99.99");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(99.99m, (decimal)v5);
    }

    [Fact]
    public void V5_ExplicitCast_ThrowsForWrongType()
    {
        // Casting a string-valued union to bool should throw
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Throws<FormatException>(() => (bool)v5);
    }

    [Fact]
    public void V5_ExplicitCast_IntFromFormatSubtype()
    {
        // The int32 format on the integer variant should produce an explicit operator int on the union
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(42, (int)v5);
    }

    [Fact]
    public void V5_ExplicitCast_IntThrowsForStringVariant()
    {
        // int cast on a string-valued union should throw
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Throws<InvalidOperationException>(() => (int)v5);
    }

    [Fact]
    public void V5_TryGetValue_SafeIntAccessor_MatchingVariant()
    {
        // TryGetValue is the non-throwing alternative to explicit cast
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out int intResult));
        Assert.Equal(42, intResult);
    }

    [Fact]
    public void V5_TryGetValue_SafeIntAccessor_NonMatchingVariant()
    {
        // For type safety, check ValueKind before calling TryGetValue
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.NotEqual(Corvus.Text.Json.JsonValueKind.Number, v5.ValueKind);
    }

    [Fact]
    public void V5_TryGetValue_SafeBoolAccessor()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out bool boolResult));
        Assert.True(boolResult);
    }

    [Fact]
    public void V5_TryGetValue_SafeStringAccessor()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out string? stringResult));
        Assert.Equal("hello", stringResult);
    }

    [Fact]
    public void V4_IsJsonString()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Assert.True(v4.IsJsonString);
        Assert.False(v4.IsJsonBoolean);
    }

    [Fact]
    public void V4_IsJsonString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.IsJsonString);
        Assert.False(v4.IsJsonBoolean);
    }

    [Fact]
    public void V5_IsStringVariant_ViaValueKind()
    {
        // V5 does not have IsJsonString — use ValueKind check instead.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, v5.ValueKind);
    }

    [Fact]
    public void V4_IsJsonBoolean()
    {
        var v4 = V4.MigrationUnion.Parse("true");
        Assert.True(v4.IsJsonBoolean);
        Assert.False(v4.IsJsonString);
    }

    [Fact]
    public void V4_IsJsonBoolean_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.IsJsonBoolean);
        Assert.False(v4.IsJsonString);
    }

    [Fact]
    public void V5_IsBoolVariant_ViaValueKind()
    {
        // V5 does not have IsJsonBoolean — use ValueKind check instead.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.True, v5.ValueKind);
    }

    [Fact]
    public void V4_TryGetAsJsonString()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Assert.True(v4.TryGetAsJsonString(out Corvus.Json.JsonString result));
        Assert.Equal("hello", (string)result);
    }

    [Fact]
    public void V4_TryGetAsJsonString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsJsonString(out Corvus.Json.JsonString result));
        Assert.Equal("hello", (string)result);
    }

    [Fact]
    public void V5_TryGetAsJsonString_StringResult()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonString(out V5.JsonString result));
        Assert.Equal("hello", (string)result);
    }

    [Fact]
    public void V4_TryGetAsJsonBoolean()
    {
        var v4 = V4.MigrationUnion.Parse("true");
        Assert.True(v4.TryGetAsJsonBoolean(out Corvus.Json.JsonBoolean result));
        Assert.True((bool)result);
    }

    [Fact]
    public void V4_TryGetAsJsonBoolean_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsJsonBoolean(out Corvus.Json.JsonBoolean result));
        Assert.True((bool)result);
    }

    [Fact]
    public void V5_TryGetAsJsonBoolean_BooleanResult()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonBoolean(out V5.JsonBoolean result));
        Assert.True((bool)result);
    }

    [Fact]
    public void V4_TryGetAsOneOf1Entity()
    {
        var v4 = V4.MigrationUnion.Parse("42");
        Assert.True(v4.TryGetAsOneOf1Entity(out V4.MigrationUnion.OneOf1Entity result));
        Assert.Equal(System.Text.Json.JsonValueKind.Number, result.ValueKind);
    }

    [Fact]
    public void V4_TryGetAsOneOf1Entity_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetAsOneOf1Entity(out V4.MigrationUnion.OneOf1Entity result));
        Assert.Equal(System.Text.Json.JsonValueKind.Number, result.ValueKind);
    }

    [Fact]
    public void V5_TryGetAsJsonInt32_NumberResult()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetAsJsonInt32(out V5.JsonInt32 result));
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Number, result.ValueKind);
    }

    [Fact]
    public void V4_GetString()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        string? value = v4.GetString();
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_GetString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        string? value = v4.GetString();
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V5_GetString_ViaExplicitCast()
    {
        // V5: use explicit cast to extract string — equivalent to V4 GetString().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        string value = (string)v5;
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_GetBoolean()
    {
        var v4 = V4.MigrationUnion.Parse("true");
        bool? value = v4.GetBoolean();
        Assert.True(value);
    }

    [Fact]
    public void V4_GetBoolean_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        bool? value = v4.GetBoolean();
        Assert.True(value);
    }

    [Fact]
    public void V5_GetBoolean_ViaExplicitCast()
    {
        // V5: use explicit cast to extract bool — equivalent to V4 GetBoolean().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        bool value = (bool)v5;
        Assert.True(value);
    }

    [Fact]
    public void V4_TryGetString()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Assert.True(v4.TryGetString(out string? value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_TryGetString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetString(out string? value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V4_TryGetBoolean()
    {
        var v4 = V4.MigrationUnion.Parse("true");
        Assert.True(v4.TryGetBoolean(out bool result));
        Assert.True(result);
    }

    [Fact]
    public void V4_TryGetBoolean_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.TryGetBoolean(out bool result));
        Assert.True(result);
    }

    [Fact]
    public void V4_EqualsString()
    {
        var v4 = V4.MigrationUnion.Parse("\"hello\"");
        Assert.True(v4.EqualsString("hello"));
        Assert.False(v4.EqualsString("world"));
    }

    [Fact]
    public void V4_EqualsString_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Assert.True(v4.EqualsString("hello"));
        Assert.False(v4.EqualsString("world"));
    }

    [Fact]
    public void V5_EqualsString_ViaEquals()
    {
        // V5: use Equals<T>() for string comparison — equivalent to V4 EqualsString().
        using var parsedV5A = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        using var parsedV5B = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        using var parsedV5C = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"world\"");

        Assert.True(parsedV5A.RootElement.Equals(parsedV5B.RootElement));
        Assert.False(parsedV5A.RootElement.Equals(parsedV5C.RootElement));
    }

    [Fact]
    public void V4_CompositionAccessor_AsString()
    {
        // V4: composition accessor returns an intermediate well-known type, then cast to primitive.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonString asString = v4.AsString;
        Assert.Equal("hello", (string)asString);
    }

    [Fact]
    public void V4_CompositionAccessor_AsNumber()
    {
        // V4: AsNumber returns a JsonNumber; cast to int for the primitive value.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonNumber asNumber = v4.AsNumber;
        Assert.Equal(42, (int)asNumber);
    }

    [Fact]
    public void V4_CompositionAccessor_AsBoolean()
    {
        // V4: AsBoolean returns a JsonBoolean; cast to bool for the primitive value.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        V4.MigrationUnion v4 = parsedV4.Instance;
        Corvus.Json.JsonBoolean asBool = v4.AsBoolean;
        Assert.True((bool)asBool);
    }

    [Fact]
    public void V5_DirectValueAccess_String_TryGetValue()
    {
        // V5: TryGetValue(out string?) directly on the union type — no intermediate type needed.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out string? value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void V5_DirectValueAccess_String_GetString()
    {
        // V5: GetString() directly on the union type.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal("hello", v5.GetString());
    }

    [Fact]
    public void V5_DirectValueAccess_String_ExplicitCast()
    {
        // V5: explicit cast (string)v5 directly on the union type.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal("hello", (string)v5);
    }

    [Fact]
    public void V5_DirectValueAccess_Number_TryGetValue()
    {
        // V5: TryGetValue(out long) directly on the union type — no intermediate type needed.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out long value));
        Assert.Equal(42L, value);
    }

    [Fact]
    public void V5_DirectValueAccess_Number_ExplicitCastInt()
    {
        // V5: explicit cast (int)v5 directly on the union type.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.Equal(42, (int)v5);
    }

    [Fact]
    public void V5_DirectValueAccess_Boolean_TryGetValue()
    {
        // V5: TryGetValue(out bool) directly on the union type — no intermediate type needed.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetValue(out bool value));
        Assert.True(value);
    }

    [Fact]
    public void V5_DirectValueAccess_Boolean_ExplicitCast()
    {
        // V5: explicit cast (bool)v5 directly on the union type.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        V5.MigrationUnion v5 = parsedV5.RootElement;
        Assert.True((bool)v5);
    }

    [Fact]
    public void BothEngines_DirectValueAccess_SameResults()
    {
        // V4 uses composition accessors (AsString/AsNumber/AsBoolean), V5 accesses values directly.
        // Both produce the same primitive results.

        // String
        using var parsedV4Str = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("\"hello\"");
        using var parsedV5Str = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("\"hello\"");
        Assert.Equal((string)parsedV4Str.Instance.AsString, (string)parsedV5Str.RootElement);

        // Number
        using var parsedV4Num = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("42");
        using var parsedV5Num = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("42");
        Assert.Equal((int)parsedV4Num.Instance.AsNumber, (int)parsedV5Num.RootElement);

        // Boolean
        using var parsedV4Bool = Corvus.Json.ParsedValue<V4.MigrationUnion>.Parse("true");
        using var parsedV5Bool = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationUnion>.Parse("true");
        Assert.Equal((bool)parsedV4Bool.Instance.AsBoolean, (bool)parsedV5Bool.RootElement);
    }
}