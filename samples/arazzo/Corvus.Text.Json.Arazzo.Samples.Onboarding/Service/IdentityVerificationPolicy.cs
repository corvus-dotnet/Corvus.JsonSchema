// <copyright file="IdentityVerificationPolicy.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// The onboarding service's KYC (know-your-customer) identity-verification policy. It derives a verification
/// verdict from the submitted applicant rather than returning a canned answer: it screens the name against a
/// watchlist, scores the match, and emits the corresponding identity document and resolved applicant.
/// </summary>
/// <remarks>
/// The rules are deliberately simple but real enough to exercise every branch the onboarding workflow cares about:
/// a name flagged on the watchlist is <c>blocked</c> (drives the fault/handled-error demos); a name marked
/// <c>transient</c> fails its first check with a low score and clears on a re-check (drives the retry-with-backoff
/// timer-suspend demo); everyone else clears. The verdicts are deterministic per applicant so the demo is
/// reproducible. A production policy would call a real identity provider here; the shape of the result is identical.
/// </remarks>
public sealed class IdentityVerificationPolicy
{
    private static readonly string[] Watchlist = ["sanction", "terror", "laundering"];
    private readonly ConcurrentDictionary<string, int> attempts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Evaluates an applicant and produces the identity outcome to persist and return.</summary>
    /// <param name="accountId">The account being verified (keys the transient re-check counter).</param>
    /// <param name="fullName">The submitted applicant name.</param>
    /// <param name="documentNumber">The submitted identity-document number, if any.</param>
    /// <param name="reviewedAt">The moment of the review.</param>
    /// <returns>The composed identity and applicant documents, the resulting status, and the resolved name.</returns>
    public IdentityOutcome Evaluate(string accountId, string fullName, string? documentNumber, DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(fullName);

        bool watchlisted = Array.Exists(Watchlist, term => fullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        bool transientApplicant = fullName.Contains("transient", StringComparison.OrdinalIgnoreCase);
        int attempt = transientApplicant ? this.attempts.AddOrUpdate(accountId, 1, static (_, n) => n + 1) : 0;

        bool verified;
        double score;
        string status;
        string[] flags;
        if (watchlisted)
        {
            // A watchlist hit is a terminal block: the workflow must not provision.
            (verified, score, status, flags) = (false, 0.42, "blocked", ["sanctions"]);
        }
        else if (transientApplicant && attempt <= 1)
        {
            // First pass for a transient applicant: an incomplete, low-confidence result that a re-check will clear.
            (verified, score, status, flags) = (false, 0.31, "created", []);
        }
        else
        {
            // A clean applicant clears with a plausible, deterministic confidence score.
            (verified, score, status, flags) = (true, Math.Round(0.85 + ((StableHash(fullName) % 15) / 100.0), 2), "verified", []);
        }

        string resolvedDocumentNumber = string.IsNullOrWhiteSpace(documentNumber) ? DeriveDocumentNumber(fullName) : documentNumber;
        string email = DeriveEmail(fullName);
        string dateOfBirth = DeriveDateOfBirth(fullName);
        string expiry = reviewedAt.AddYears(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        byte[] applicant = OnboardingJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("fullName", fullName);
            writer.WriteString("dateOfBirth", dateOfBirth);
            writer.WriteString("email", email);
            writer.WriteString("country", "GB");
            writer.WriteEndObject();
        });

        byte[] identity = OnboardingJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("verified", verified);
            writer.WriteNumber("score", score);
            writer.WriteString("method", "document");
            writer.WriteString("reviewedAt", reviewedAt);
            OnboardingJson.WriteDocumentProperty(writer, "applicant", applicant);
            writer.WriteStartArray("flags");
            foreach (string flag in flags)
            {
                writer.WriteStringValue(flag);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("evidence");
            writer.WriteStartObject();
            writer.WriteString("kind", "document");
            writer.WriteString("documentType", "passport");
            writer.WriteString("documentNumber", resolvedDocumentNumber);
            writer.WriteString("expiry", expiry);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

        return new IdentityOutcome(identity, applicant, status, fullName);
    }

    private static uint StableHash(string value) => OnboardingJson.StableHash(value);

    private static string DeriveDocumentNumber(string fullName)
        => string.Create(CultureInfo.InvariantCulture, $"P{(StableHash(fullName) % 9000000) + 1000000}");

    private static string DeriveDateOfBirth(string fullName)
    {
        uint h = StableHash(fullName);
        int year = 1960 + (int)(h % 45);
        int month = 1 + (int)((h / 45) % 12);
        int day = 1 + (int)((h / 540) % 28);
        return new DateOnly(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string DeriveEmail(string fullName)
    {
        var local = new StringBuilder(fullName.Length);
        foreach (char c in fullName.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                local.Append(c);
            }
            else if (c is ' ' or '.' && local.Length > 0 && local[^1] != '.')
            {
                local.Append('.');
            }
        }

        string user = local.Length > 0 ? local.ToString().Trim('.') : "applicant";
        return string.Concat(user, "@example.com");
    }
}

/// <summary>
/// The outcome of an identity verification: the composed identity and applicant wire documents, the resulting
/// account status, and the resolved applicant name (for display and search).
/// </summary>
/// <param name="IdentityBytes">The identity-result document (JSON).</param>
/// <param name="ApplicantBytes">The resolved applicant document (JSON).</param>
/// <param name="Status">The resulting account status.</param>
/// <param name="FullName">The resolved applicant name.</param>
public readonly record struct IdentityOutcome(byte[] IdentityBytes, byte[] ApplicantBytes, string Status, string FullName);
