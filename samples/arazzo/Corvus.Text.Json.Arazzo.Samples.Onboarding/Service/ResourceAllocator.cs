// <copyright file="ResourceAllocator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// Allocates the tenant resources for a newly verified account: a per-account database and asset bucket, a quota,
/// and an access URL. The allocation is deterministic per account (so the demo is reproducible) but genuinely
/// per-account — every onboarded customer gets its own named resources and endpoints, not a shared canned result.
/// </summary>
/// <remarks>
/// A production allocator would call the cloud provider's control plane here; the shape of the provisioning result
/// is identical, so the onboarding workflow's provision step is unchanged.
/// </remarks>
public sealed class ResourceAllocator
{
    /// <summary>Allocates resources for an account and composes the provisioning document.</summary>
    /// <param name="accountId">The account to provision for.</param>
    /// <returns>The provisioning document (JSON).</returns>
    public byte[] Allocate(string accountId)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        string shortId = accountId.Length >= 8 ? accountId[..8] : accountId;
        uint h = OnboardingJson.StableHash(accountId);
        int quotaGb = 10 + (int)(h % 91);
        string plan = quotaGb >= 64 ? "scale" : "standard";
        const string region = "eu-west-1";

        return OnboardingJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("accountUrl", string.Concat("https://app.onboarding.example/accounts/", accountId));
            writer.WriteNumber("quotaGb", quotaGb);

            writer.WriteStartArray("resources");
            WriteResource(writer, "database", string.Concat("db-", shortId), region, string.Concat("https://db.onboarding.example/", shortId));
            WriteResource(writer, "bucket", string.Concat("assets-", shortId), region, string.Concat("https://storage.onboarding.example/", shortId, "/assets"));
            writer.WriteEndArray();

            writer.WritePropertyName("tags");
            writer.WriteStartObject();
            writer.WriteString("env", "production");
            writer.WriteString("plan", plan);
            writer.WriteString("region", region);
            writer.WriteEndObject();

            writer.WriteEndObject();
        });

        static void WriteResource(Utf8JsonWriter writer, string kind, string name, string region, string endpoint)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind);
            writer.WriteString("name", name);
            writer.WriteString("region", region);
            writer.WriteString("endpoint", endpoint);
            writer.WriteEndObject();
        }
    }
}
