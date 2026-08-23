// <copyright file="RedisSecurityLabels.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// The Redis half of the §14.4 security-label narrowing: each label is a Redis SET of row ids, and a reach plan
/// resolves to its candidate ids in one read-only Lua evaluation that runs the compiled
/// <see cref="SecurityLabelQuery"/> program server-side — the set operations native to the backend, so only the
/// final candidate set ever crosses the wire.
/// </summary>
/// <remarks>
/// <para>
/// Two sets per tag, mirroring the other label indexes: <c>{prefix}v:{keyByteLength}:{key}{value}</c> for the
/// (key, value) label and <c>{prefix}k:{key}</c> for the key-only entry that serves "any value of this key".
/// Redis keys are binary-safe, so no encoding is needed; the key's <b>UTF-8 byte length</b> prefixes the
/// concatenation so the pair maps injectively — without it, the tag ("a", "bc") and the tag ("ab", "c") would
/// share a set, and a crafted tag could widen another tag's reach. The byte length is what Lua's <c>#</c>
/// operator yields for the decoded tag strings, so the maintenance script and this class build identical names.
/// </para>
/// <para>
/// Set maintenance lives with each store's write paths (the run store diffs the label sets inside its atomic
/// save script; this class only reads). A stale entry — a label pointing at a row that no longer carries it —
/// is harmless by the §14.4 contract, because the caller evaluates the exact reach over every candidate it
/// loads; a missing entry would hide a row, which is why writers add entries before a row becomes visible and
/// remove them after it stops being.
/// </para>
/// </remarks>
internal static class RedisSecurityLabels
{
    /// <summary>The label-set key prefix for the run store's row ids.</summary>
    internal const string RunLabelPrefix = "arazzo:label:";

    /// <summary>The label-set key prefix for the catalog store's row ids (the <c>{base}{version:D10}</c> sort key).</summary>
    internal const string CatalogLabelPrefix = "arazzo:catalog:label:";

    /// <summary>The label-set key prefix for the observed-identity store's row ids (the <c>{value}\0{kind}</c> sort key).</summary>
    internal const string ObservedIdentityLabelPrefix = "arazzo:obid:label:";

    /// <summary>The label-set key prefix for the environment store's identity tuples (the all-index members).</summary>
    internal const string EnvironmentLabelPrefix = "arazzo:env:label:";

    /// <summary>The label-set key prefix for the source store's identity tuples (the all-index members).</summary>
    internal const string SourceLabelPrefix = "arazzo:source:label:";

    /// <summary>The label-set key prefix for the source-credential store's identity tuples (the all-index members).</summary>
    internal const string SourceCredentialLabelPrefix = "arazzo:scred:label:";

    /// <summary>The label-set key prefix for the workspace-workflow store's working-copy ids.</summary>
    internal const string WorkspaceWorkflowLabelPrefix = "arazzo:wc:label:";

    /// <summary>The label-set key prefix for the security-policy rule store's row ids (the rule name). Kept separate from
    /// the binding prefix because rule names and binding ids are independent id spaces that could otherwise collide.</summary>
    internal const string SecurityRuleLabelPrefix = "arazzo:secrule:label:";

    /// <summary>The label-set key prefix for the security-policy binding store's row ids (the binding id).</summary>
    internal const string SecurityBindingLabelPrefix = "arazzo:secbinding:label:";

    // Interprets the compiled plan program over a stack of Lua tables, entirely server-side. ARGV is the
    // postfix token stream: 'S' <set key> pushes a label set's members; 'U' <n> / 'I' <n> pop n sets and push
    // their union / intersection. Read-only — no temp keys to clean up, safe on a replica — and the whole
    // resolution is one round trip whatever the plan's shape. An intersection that empties stops combining
    // early; nothing can re-enter it.
    private const string ResolveScript =
        """
        local stack = {}
        local top = 0
        local i = 1
        while i <= #ARGV do
            local op = ARGV[i]
            if op == 'S' then
                local set = {}
                for _, m in ipairs(redis.call('SMEMBERS', ARGV[i + 1])) do set[m] = true end
                top = top + 1
                stack[top] = set
            elseif op == 'U' then
                local n = tonumber(ARGV[i + 1])
                local merged = stack[top - n + 1]
                for j = top - n + 2, top do
                    for m in pairs(stack[j]) do merged[m] = true end
                end
                top = top - n + 1
                stack[top] = merged
            else
                local n = tonumber(ARGV[i + 1])
                local result = stack[top - n + 1]
                for j = top - n + 2, top do
                    if next(result) == nil then break end
                    local narrowed = {}
                    local s = stack[j]
                    for m in pairs(result) do if s[m] then narrowed[m] = true end end
                    result = narrowed
                end
                top = top - n + 1
                stack[top] = result
            end
            i = i + 2
        end
        local out = {}
        for m in pairs(stack[1]) do out[#out + 1] = m end
        return out
        """;

    /// <summary>
    /// Resolves the reach to the candidate row ids the label sets admit (§14.4). Null means "the index could not
    /// narrow this rule", which is a legitimate answer — the exact evaluation downstream still decides what the
    /// principal sees, so an un-narrowable rule costs throughput and never widens reach. An empty set is not
    /// null: no row qualifies, and the caller should answer without reading anything.
    /// </summary>
    /// <param name="database">The Redis database.</param>
    /// <param name="labelPrefix">The store's label-set key prefix (one of the <c>*LabelPrefix</c> constants).</param>
    /// <param name="security">The reach filter, or <see langword="null"/> for an unrestricted query.</param>
    /// <returns>The candidate ids (a superset of the rows the rule admits), the empty set, or <see langword="null"/> for no narrowing.</returns>
    internal static async ValueTask<IReadOnlySet<string>?> ResolveReachCandidatesAsync(IDatabase database, string labelPrefix, SecurityFilter? security)
    {
        if (security is null)
        {
            return null;
        }

        IReadOnlyList<SecurityLabelInstruction>? program = security.ToPredicate(SecurityLabelQueryEmitter.Instance).Compile();
        if (program is null)
        {
            return null;
        }

        var candidates = new HashSet<string>(StringComparer.Ordinal);
        if (program.Count == 0)
        {
            return candidates;
        }

        var argv = new RedisValue[program.Count * 2];
        int position = 0;
        foreach (SecurityLabelInstruction instruction in program)
        {
            switch (instruction.Kind)
            {
                case SecurityLabelInstructionKind.PushLabel:
                    argv[position++] = "S";
                    argv[position++] = LabelSetKey(labelPrefix, instruction.Key!, instruction.Value!);
                    break;

                case SecurityLabelInstructionKind.PushKey:
                    argv[position++] = "S";
                    argv[position++] = KeySetKey(labelPrefix, instruction.Key!);
                    break;

                case SecurityLabelInstructionKind.Union:
                    argv[position++] = "U";
                    argv[position++] = instruction.Count;
                    break;

                default:
                    argv[position++] = "I";
                    argv[position++] = instruction.Count;
                    break;
            }
        }

        RedisResult result = await database.ScriptEvaluateAsync(ResolveScript, keys: null, argv).ConfigureAwait(false);
        foreach (RedisResult id in (RedisResult[])result!)
        {
            candidates.Add((string)id!);
        }

        return candidates;
    }

    /// <summary>The label-set keys a row's tags occupy — two per tag, deduplicated — for client-side teardown.</summary>
    /// <param name="labelPrefix">The store's label-set key prefix.</param>
    /// <param name="securityTags">The row's security tags.</param>
    /// <returns>The distinct set keys.</returns>
    internal static HashSet<string> SetKeysFor(string labelPrefix, in SecurityTagSet securityTags)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SecurityTag tag in securityTags)
        {
            keys.Add(LabelSetKey(labelPrefix, tag.Key, tag.Value));
            keys.Add(KeySetKey(labelPrefix, tag.Key));
        }

        return keys;
    }

    /// <summary>Builds the set key for a (key, value) label. Must match the maintenance Lua byte-for-byte.</summary>
    /// <param name="labelPrefix">The store's label-set key prefix.</param>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The set key.</returns>
    internal static string LabelSetKey(string labelPrefix, string key, string value)
        => $"{labelPrefix}v:{Encoding.UTF8.GetByteCount(key)}:{key}{value}";

    /// <summary>Builds the set key for the key-only entry. Must match the maintenance Lua byte-for-byte.</summary>
    /// <param name="labelPrefix">The store's label-set key prefix.</param>
    /// <param name="key">The tag key.</param>
    /// <returns>The set key.</returns>
    internal static string KeySetKey(string labelPrefix, string key)
        => labelPrefix + "k:" + key;
}