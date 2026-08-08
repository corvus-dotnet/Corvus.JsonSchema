// <copyright file="AzureStorageSecurityLabelIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using Azure;
using Azure.Data.Tables;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// The Azure Table implementation of <see cref="ISecurityLabelIndex"/> (design §14.4): a table whose
/// PartitionKey is a security-tag label and whose RowKey is the row id, so resolving "which rows carry this
/// label" is a single-partition query — the only genuinely indexed access Table storage offers, since every
/// property other than PartitionKey and RowKey is a scan.
/// </summary>
/// <remarks>
/// <para>
/// Each tag writes <b>two</b> entries: one under the (key, value) label and one under a key-only label, so
/// <see cref="LookupKeyAsync"/> is also a partition query rather than a prefix range over the whole table. The
/// key-only entry is shared by every value of that key on the same row, so upserting it repeatedly is idempotent.
/// </para>
/// <para>
/// Labels are user-authored, and PartitionKey forbids <c>/</c>, <c>\</c>, <c>#</c>, <c>?</c> and control
/// characters, so the parts are Base64Url-encoded. That is injective, keeps the key within the allowed
/// character set without an escaping scheme to get wrong, and makes it impossible for a crafted tag to collide
/// with another tag's partition — a label carrying the separator character cannot forge a different label.
/// </para>
/// </remarks>
/// <param name="table">The label table.</param>
public sealed class AzureStorageSecurityLabelIndex(TableClient table) : ISecurityLabelIndex
{
    // Distinct prefixes rather than "key part, optional value part", so a tag with an EMPTY value cannot encode
    // to the same token as the key-only entry for that key.
    private const string KeyOnlyPrefix = "k~";
    private const string LabelPrefix = "v~";

    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<string>> LookupLabelAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return this.LookupPartitionAsync(LabelToken(key, value), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyCollection<string>> LookupKeyAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return this.LookupPartitionAsync(KeyToken(key), cancellationToken);
    }

    /// <summary>Builds the partition token for a (key, value) label.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The partition key.</returns>
    internal static string LabelToken(string key, string value)
        => string.Concat(LabelPrefix, Encode(key), "~", Encode(value));

    /// <summary>Builds the partition token for the key-only entry.</summary>
    /// <param name="key">The tag key.</param>
    /// <returns>The partition key.</returns>
    internal static string KeyToken(string key) => KeyOnlyPrefix + Encode(key);

    /// <summary>The partition tokens a row's tags occupy — two per tag, deduplicated.</summary>
    /// <param name="securityTags">The row's security tags.</param>
    /// <returns>The distinct partition keys.</returns>
    internal static HashSet<string> TokensFor(in SecurityTagSet securityTags)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (SecurityTag tag in securityTags)
        {
            tokens.Add(LabelToken(tag.Key, tag.Value));
            tokens.Add(KeyToken(tag.Key));
        }

        return tokens;
    }

    private static string Encode(string text)
    {
        // Sized from the UTF-8 byte count rather than the char count, so a multi-byte tag is not truncated.
        int byteCount = Encoding.UTF8.GetByteCount(text);
        byte[]? rented = byteCount > 256 ? new byte[byteCount] : null;
        Span<byte> utf8 = rented ?? stackalloc byte[256];
        int written = Encoding.UTF8.GetBytes(text, utf8);
        return Base64Url.EncodeToString(utf8[..written]);
    }

    private async ValueTask<IReadOnlyCollection<string>> LookupPartitionAsync(string partition, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        AsyncPageable<TableEntity> entities = table.QueryAsync<TableEntity>(
            TableClient.CreateQueryFilter($"PartitionKey eq {partition}"),
            select: TableStorageRowKeyOnly,
            cancellationToken: cancellationToken);

        await foreach (TableEntity entity in entities.ConfigureAwait(false))
        {
            ids.Add(entity.RowKey);
        }

        return ids;
    }

    // Only the row id is needed, so the entries never carry their payload back across the wire.
    private static readonly string[] TableStorageRowKeyOnly = ["RowKey"];
}