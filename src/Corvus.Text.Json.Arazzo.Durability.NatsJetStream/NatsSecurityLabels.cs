// <copyright file="NatsSecurityLabels.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// The NATS JetStream half of the §14.4 security-label narrowing: one key/value bucket per store holds the
/// label entries, keyed so that "which rows carry this label" is a <b>subject-filtered key listing</b> — the
/// closest thing to an indexed access JetStream offers, since a KV key is a subject and the server filters
/// subjects, not values.
/// </summary>
/// <remarks>
/// <para>
/// The design sketch said "a KV bucket per label", but labels are user-authored and appear at runtime, while
/// creating a bucket creates a JetStream stream — a stream-management right the store's least-privileged
/// operational account deliberately lacks (see the <c>PrepareAsync</c>/<c>ConnectAsync</c> privilege split). So
/// the labels live in ONE bucket provisioned at prepare time, and the per-label partitioning moves into the key:
/// <c>v.{key}.{value}.{rowId}</c> for a (key, value) label and <c>k.{key}.{rowId}</c> for the key-only entry
/// that serves "any value of this key", each part an encoded token. A label lookup lists <c>v.{k}.{v}.*</c>; a
/// row's own entries (for save-diff and delete teardown) list <c>v.*.*.{id}</c> and <c>k.*.{id}</c>, so the
/// reverse lookup transfers a handful of keys, never a checkpoint.
/// </para>
/// <para>
/// Tag keys, values and row ids are user-authored, and a KV key's charset is restricted (and <c>.</c>, <c>*</c>
/// and <c>&gt;</c> are subject structure), so every part is Base64Url-encoded with a fixed leading character:
/// the alphabet is entirely legal in a key, <c>.</c> can never appear inside a token (which is what makes the
/// key shape injective — a crafted tag cannot forge another tag's entry or a different row's), and the leading
/// character keeps a token non-empty even for an empty part, which a subject would otherwise reject.
/// </para>
/// </remarks>
internal static class NatsSecurityLabels
{
    /// <summary>The label bucket for the run store's row ids.</summary>
    internal const string RunsLabelBucket = "arazzo_labels";

    /// <summary>The label bucket for the catalog store's row ids (the <c>{base}{version:D10}</c> sort key).</summary>
    internal const string CatalogLabelBucket = "arazzo_catalog_labels";

    /// <summary>The label bucket for the environment store's row ids (the store's KV keys).</summary>
    internal const string EnvironmentLabelBucket = "arazzo_environment_labels";

    /// <summary>The label bucket for the source store's row ids (the store's KV keys).</summary>
    internal const string SourceLabelBucket = "arazzo_source_labels";

    /// <summary>The label bucket for the source-credential store's row ids (the store's KV keys).</summary>
    internal const string SourceCredentialLabelBucket = "arazzo_source_credential_labels";

    /// <summary>The label bucket for the workspace-workflow store's row ids (the store's KV keys).</summary>
    internal const string WorkspaceWorkflowLabelBucket = "arazzo_workspace_workflow_labels";

    /// <summary>The label bucket for the security-policy rule store's row ids (the rule name). Kept separate from the
    /// binding bucket because rule names and binding ids are independent id spaces that could otherwise collide.</summary>
    internal const string SecurityRuleLabelBucket = "arazzo_security_rule_labels";

    /// <summary>The label bucket for the security-policy binding store's row ids (the binding id).</summary>
    internal const string SecurityBindingLabelBucket = "arazzo_security_binding_labels";

    /// <summary>The entry key for a (key, value) label on a row.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <param name="rowId">The row id.</param>
    /// <returns>The entry key.</returns>
    internal static string LabelEntryKey(string key, string value, string rowId)
        => $"v.{Token(key)}.{Token(value)}.{Token(rowId)}";

    /// <summary>The entry key for the key-only entry on a row.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="rowId">The row id.</param>
    /// <returns>The entry key.</returns>
    internal static string KeyEntryKey(string key, string rowId)
        => $"k.{Token(key)}.{Token(rowId)}";

    /// <summary>The key-listing filter for every row carrying the (key, value) label.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The subject filter.</returns>
    internal static string LabelFilter(string key, string value)
        => $"v.{Token(key)}.{Token(value)}.*";

    /// <summary>The key-listing filter for every row carrying the tag key with any value.</summary>
    /// <param name="key">The tag key.</param>
    /// <returns>The subject filter.</returns>
    internal static string KeyFilter(string key)
        => $"k.{Token(key)}.*";

    /// <summary>The key-listing filters for a row's own entries — the reverse lookup a save-diff or delete uses.</summary>
    /// <param name="rowId">The row id.</param>
    /// <returns>The subject filters.</returns>
    internal static string[] RowFilters(string rowId)
    {
        string token = Token(rowId);
        return [$"v.*.*.{token}", $"k.*.{token}"];
    }

    /// <summary>The entry keys a row's tags occupy — two per tag, deduplicated.</summary>
    /// <param name="securityTags">The row's security tags.</param>
    /// <param name="rowId">The row id.</param>
    /// <returns>The distinct entry keys.</returns>
    internal static HashSet<string> EntryKeysFor(in SecurityTagSet securityTags, string rowId)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SecurityTag tag in securityTags)
        {
            keys.Add(LabelEntryKey(tag.Key, tag.Value, rowId));
            keys.Add(KeyEntryKey(tag.Key, rowId));
        }

        return keys;
    }

    /// <summary>Recovers the row id from an entry key's final token.</summary>
    /// <param name="entryKey">The entry key.</param>
    /// <returns>The row id.</returns>
    internal static string DecodeRowId(string entryKey)
    {
        // The row id is always the last token, and tokens cannot contain '.'.
        ReadOnlySpan<char> token = entryKey.AsSpan(entryKey.LastIndexOf('.') + 1);
        return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token[1..]));
    }

    private static string Token(string part)
    {
        // Sized from the UTF-8 byte count rather than the char count, so a multi-byte tag is not truncated. The
        // leading character keeps the token non-empty for an empty part.
        int byteCount = Encoding.UTF8.GetByteCount(part);
        byte[]? rented = byteCount > 256 ? new byte[byteCount] : null;
        Span<byte> utf8 = rented ?? stackalloc byte[256];
        int written = Encoding.UTF8.GetBytes(part, utf8);
        return "x" + Base64Url.EncodeToString(utf8[..written]);
    }
}

/// <summary>
/// The NATS implementation of <see cref="ISecurityLabelIndex"/> over a labels bucket: each lookup is one
/// subject-filtered key listing, resolved through the shared <see cref="SecurityLabelQueryResolver"/> like the
/// other point-lookup backends.
/// </summary>
/// <param name="labels">The labels bucket.</param>
internal sealed class NatsSecurityLabelIndex(INatsKVStore labels) : ISecurityLabelIndex
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<string>> LookupLabelAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return this.CollectAsync(NatsSecurityLabels.LabelFilter(key, value), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<string>> LookupKeyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return this.CollectAsync(NatsSecurityLabels.KeyFilter(key), cancellationToken);
    }

    private async ValueTask<IReadOnlyCollection<string>> CollectAsync(string filter, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        await foreach (string entryKey in labels.GetKeysAsync([filter], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            ids.Add(NatsSecurityLabels.DecodeRowId(entryKey));
        }

        return ids;
    }
}