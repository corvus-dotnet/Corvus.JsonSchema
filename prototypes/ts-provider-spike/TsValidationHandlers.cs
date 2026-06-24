using System.Text;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// TS emission seam: a handler that the registry dispatches to also implements this to emit TS.
// (The core IKeywordValidationHandler is pure dispatch — priority + HandlesKeyword — so emission
// is a provider-level concern, exactly as the C# provider's handlers implement C#-emit interfaces.)
internal interface ITsKeywordEmitter
{
    void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword);
}

internal static class TsEmit
{
    // A correctly-escaped JS/TS string literal (handles control chars, quotes, backslashes, non-ASCII)
    // — a JSON string literal is a valid JS string literal.
    public static string Str(string name) => JsonSerializer.Serialize(name);

    public static List<string> ReadTypes(JsonElement typeValue)
    {
        var r = new List<string>();
        if (typeValue.ValueKind == JsonValueKind.String) { r.Add(typeValue.GetString()!); }
        else if (typeValue.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement i in typeValue.EnumerateArray())
            {
                if (i.ValueKind == JsonValueKind.String) { r.Add(i.GetString()!); }
            }
        }

        return r;
    }

    public static string KindExpr(string t) => t switch
    {
        "object" => "(typeof value === \"object\" && value !== null && !Array.isArray(value))",
        "array" => "Array.isArray(value)",
        "string" => "typeof value === \"string\"",
        "number" => "typeof value === \"number\"",
        "integer" => "(typeof value === \"number\" && Number.isInteger(value))",
        "boolean" => "typeof value === \"boolean\"",
        "null" => "value === null",
        _ => "true",
    };
}

internal sealed class TsTypeHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 100;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "type";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val)) { return; }
        List<string> kinds = TsEmit.ReadTypes(val);
        if (kinds.Count > 0)
        {
            sb.Append("  if (!(").Append(string.Join(" || ", kinds.ConvertAll(TsEmit.KindExpr))).Append(")) { return false; }\n");
        }
    }
}

internal sealed class TsStringLengthHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword is "minLength" or "maxLength";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Number) { return; }
        string op = keyword.Keyword == "minLength" ? "<" : ">";
        sb.Append("  if (typeof value === \"string\" && [...value].length ").Append(op).Append(' ').Append(val.GetRawText()).Append(") { return false; }\n");
    }
}

internal sealed class TsArrayItemCountHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword is "minItems" or "maxItems";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Number) { return; }
        string op = keyword.Keyword == "minItems" ? "<" : ">";
        sb.Append("  if (Array.isArray(value) && value.length ").Append(op).Append(' ').Append(val.GetRawText()).Append(") { return false; }\n");
    }
}

internal sealed class TsNumberRangeHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword is "minimum" or "maximum";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Number) { return; }
        string op = keyword.Keyword == "minimum" ? "<" : ">";
        sb.Append("  if (typeof value === \"number\" && value ").Append(op).Append(' ').Append(val.GetRawText()).Append(") { return false; }\n");
    }
}

internal sealed class TsPatternHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "pattern";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (td.TryGetKeyword(keyword, out JsonElement val) && val.ValueKind == JsonValueKind.String)
        {
            sb.Append("  if (typeof value === \"string\" && !new RegExp(").Append(val.GetRawText()).Append(", \"u\").test(value)) { return false; }\n");
        }
    }
}

internal sealed class TsEnumHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "enum";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (td.TryGetKeyword(keyword, out JsonElement val) && val.ValueKind == JsonValueKind.Array)
        {
            sb.Append("  { const allowed: readonly unknown[] = ").Append(val.GetRawText()).Append("; if (!allowed.some((a) => JSON.stringify(a) === JSON.stringify(value))) { return false; } }\n");
        }
    }
}

internal sealed class TsPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 800;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "properties";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (td.PropertyDeclarations.Count == 0) { return; }
        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string key = TsEmit.Str(p.JsonPropertyName);
            if (p.ReducedPropertyType.TryGetMetadata<string>("Ts_FinalName", out string? pn) && !string.IsNullOrEmpty(pn))
            {
                sb.Append("    if (Object.prototype.hasOwnProperty.call(o, ").Append(key).Append(") && !evaluate").Append(pn).Append("(o[").Append(key).Append("])) { return false; }\n");
            }
        }

        sb.Append("  }\n");
    }
}

// `required` as its own handler (independent of `properties`) — reads the required array directly,
// so a schema with `required` but no `properties` still enforces presence.
internal sealed class TsRequiredHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 700;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "required";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (!td.TryGetKeyword(keyword, out JsonElement val) || val.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var names = new List<string>();
        foreach (JsonElement n in val.EnumerateArray())
        {
            if (n.ValueKind == JsonValueKind.String)
            {
                names.Add(n.GetString()!);
            }
        }

        if (names.Count == 0)
        {
            return;
        }

        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        foreach (string name in names)
        {
            sb.Append("    if (!Object.prototype.hasOwnProperty.call(value, ").Append(TsEmit.Str(name)).Append(")) { return false; }\n");
        }

        sb.Append("  }\n");
    }
}

// patternProperties: every OWN key matching a pattern must validate against that pattern's subschema.
// (A key may also be in `properties` and/or feed `additionalProperties` — those are separate handlers.)
internal sealed class TsPatternPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 810;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "patternProperties";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is null)
        {
            return;
        }

        var entries = new List<(string Pattern, string Name)>();
        foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
        {
            foreach (PatternPropertyDeclaration d in kv.Value)
            {
                if (d.ReducedPatternPropertyType.TryGetMetadata<string>("Ts_FinalName", out string? n) && !string.IsNullOrEmpty(n))
                {
                    entries.Add((d.Pattern, n!));
                }
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    for (const k of Object.keys(o)) {\n");
        foreach ((string pattern, string name) in entries)
        {
            sb.Append("      if (new RegExp(").Append(TsEmit.Str(pattern)).Append(", \"u\").test(k) && !evaluate").Append(name).Append("(o[k])) { return false; }\n");
        }

        sb.Append("    }\n  }\n");
    }
}

// additionalProperties: every OWN key not in `properties` and not matching any `patternProperties`
// pattern must validate against the additionalProperties subschema (which is the `false` validator
// when additionalProperties is false, rejecting any such key).
internal sealed class TsAdditionalPropertiesHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 820;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "additionalProperties";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        FallbackObjectPropertyType? fb = td.LocalEvaluatedPropertyType();
        if (fb is null || !fb.ReducedType.TryGetMetadata<string>("Ts_FinalName", out string? name) || string.IsNullOrEmpty(name))
        {
            return;
        }

        var known = new List<string>();
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            known.Add(TsEmit.Str(p.JsonPropertyName));
        }

        var patterns = new List<string>();
        IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>>? pp = td.PatternProperties();
        if (pp is not null)
        {
            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> kv in pp)
            {
                foreach (PatternPropertyDeclaration d in kv.Value)
                {
                    patterns.Add("new RegExp(" + TsEmit.Str(d.Pattern) + ", \"u\")");
                }
            }
        }

        sb.Append("  if (typeof value === \"object\" && value !== null && !Array.isArray(value)) {\n");
        sb.Append("    const o = value as Record<string, unknown>;\n");
        sb.Append("    const known = new Set<string>([").Append(string.Join(", ", known)).Append("]);\n");
        sb.Append("    const patterns = [").Append(string.Join(", ", patterns)).Append("];\n");
        sb.Append("    for (const k of Object.keys(o)) {\n");
        sb.Append("      if (known.has(k)) { continue; }\n");
        sb.Append("      if (patterns.some((p) => p.test(k))) { continue; }\n");
        sb.Append("      if (!evaluate").Append(name).Append("(o[k])) { return false; }\n");
        sb.Append("    }\n  }\n");
    }
}

// ---- EXTENSION: a user/third-party handler for a keyword the base provider does not handle.
// Registered at runtime via provider.RegisterValidationHandlers(...). The core registry dispatches
// to it (HandlesKeyword), proving custom keyword validation plugs in without touching the provider.
internal sealed class TsMultipleOfHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword.Keyword == "multipleOf";

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (td.TryGetKeyword(keyword, out JsonElement val) && val.ValueKind == JsonValueKind.Number)
        {
            sb.Append("  if (typeof value === \"number\" && (value % ").Append(val.GetRawText()).Append(") !== 0) { return false; } // EXTENSION (exact via BigInt in production, design 4.1)\n");
        }
    }
}
