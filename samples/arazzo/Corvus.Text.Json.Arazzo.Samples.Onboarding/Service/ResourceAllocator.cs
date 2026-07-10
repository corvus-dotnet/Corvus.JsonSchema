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

        // Composed through the pooled writer/buffer into one owned array (the store column needs a byte[]); the state is
        // carried by an `in` context so the write callback is static (no closure allocation).
        var context = new ProvisioningContext(accountId, shortId, quotaGb, plan, region);
        return OnboardingJson.ToArray<ProvisioningContext>(in context, static (writer, in c) =>
        {
            writer.WriteStartObject();
            writer.WriteString("accountUrl", string.Concat("https://app.onboarding.example/accounts/", c.AccountId));
            writer.WriteNumber("quotaGb", c.QuotaGb);

            writer.WriteStartArray("resources");
            WriteResource(writer, "database", string.Concat("db-", c.ShortId), c.Region, string.Concat("https://db.onboarding.example/", c.ShortId));
            WriteResource(writer, "bucket", string.Concat("assets-", c.ShortId), c.Region, string.Concat("https://storage.onboarding.example/", c.ShortId, "/assets"));
            writer.WriteEndArray();

            writer.WritePropertyName("tags");
            writer.WriteStartObject();
            writer.WriteString("env", "production");
            writer.WriteString("plan", c.Plan);
            writer.WriteString("region", c.Region);
            writer.WriteEndObject();

            writer.WriteEndObject();

            static void WriteResource(Utf8JsonWriter writer, string kind, string name, string region, string endpoint)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", kind);
                writer.WriteString("name", name);
                writer.WriteString("region", region);
                writer.WriteString("endpoint", endpoint);
                writer.WriteEndObject();
            }
        });
    }

    // Carries the provisioning state to the pooled compose so the write callback stays static (no closure allocation).
    private readonly record struct ProvisioningContext(string AccountId, string ShortId, int QuotaGb, string Plan, string Region);
}
