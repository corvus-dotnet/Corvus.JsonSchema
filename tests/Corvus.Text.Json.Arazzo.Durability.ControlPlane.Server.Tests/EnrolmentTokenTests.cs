// <copyright file="EnrolmentTokenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of the environment-scoped enrolment bearer token (ADR 0065 decision 2): environment binding, expiry, and
/// tamper resistance. The token is what lets a runner nobody has named register itself, so what it refuses matters as
/// much as what it admits.
/// </summary>
[TestClass]
public sealed class EnrolmentTokenTests
{
    private static readonly byte[] Secret = Encoding.UTF8.GetBytes("a-shared-enrolment-secret-of-sufficient-length");
    private static readonly byte[] OtherSecret = Encoding.UTF8.GetBytes("a-different-enrolment-secret-of-sufficient-len");
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_fresh_token_validates_for_its_environment()
    {
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(10));

        EnrolmentToken.TryValidate(Secret, token, "production", Now).ShouldBeTrue();
    }

    [TestMethod]
    public void A_token_does_not_validate_for_another_environment()
    {
        // The property that keeps registration environment-scoped. A token delivered to the staging runners must not
        // enrol anything into production, or the scoping is decorative.
        string token = EnrolmentToken.Issue(Secret, "staging", Now.AddMinutes(10));

        EnrolmentToken.TryValidate(Secret, token, "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_token_does_not_validate_under_another_secret()
    {
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(10));

        EnrolmentToken.TryValidate(OtherSecret, token, "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void An_expired_token_does_not_validate()
    {
        // Expiry is the entire bound on a leaked token, since nothing revokes one.
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(-1));

        EnrolmentToken.TryValidate(Secret, token, "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_token_expiring_exactly_now_does_not_validate()
    {
        string token = EnrolmentToken.Issue(Secret, "production", Now);

        EnrolmentToken.TryValidate(Secret, token, "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void An_extended_expiry_does_not_validate()
    {
        // The expiry travels in the clear, so the obvious attack is to edit it. It is inside the signature, so editing
        // it invalidates the token rather than extending it.
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(-1));
        string tampered = $"{Now.AddHours(1).ToUnixTimeSeconds()}.{token[(token.IndexOf('.') + 1)..]}";

        EnrolmentToken.TryValidate(Secret, tampered, "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_non_canonical_expiry_does_not_validate()
    {
        // Exactly one string admits a runner. A padded or signed variant that parses to the same instant is a different
        // token, so it does not get to be an equivalent one.
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(10));
        string signature = token[(token.IndexOf('.') + 1)..];
        long expiry = Now.AddMinutes(10).ToUnixTimeSeconds();

        EnrolmentToken.TryValidate(Secret, $"0{expiry}.{signature}", "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, $"+{expiry}.{signature}", "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, $" {expiry}.{signature}", "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_malformed_token_does_not_validate()
    {
        EnrolmentToken.TryValidate(Secret, null, "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, string.Empty, "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, "no-separator", "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, ".signature-only", "production", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, $"{Now.AddMinutes(10).ToUnixTimeSeconds()}.", "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_weak_secret_is_refused_rather_than_used()
    {
        // A short secret would still produce a plausible-looking token, so this fails loudly at the mint instead.
        byte[] tooShort = new byte[EnrolmentToken.MinimumSecretBytes - 1];

        Should.Throw<ArgumentException>(() => EnrolmentToken.Issue(tooShort, "production", Now.AddMinutes(10)));

        // Validation refuses rather than throwing: a misconfigured deployment declines enrolments instead of faulting
        // every registration request.
        EnrolmentToken.TryValidate(tooShort, EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(10)), "production", Now).ShouldBeFalse();
    }

    [TestMethod]
    public void Validating_a_token_allocates_nothing()
    {
        // Validation runs before the caller has proved anything, so its cost is what someone presenting a wrong token
        // can spend on the deployment's behalf. The first shape of this allocated 576 bytes a call, transcoding both
        // sides of the comparison and materialising the expiry twice; this pins the rewrite rather than trusting it.
        string token = EnrolmentToken.Issue(Secret, "production", Now.AddMinutes(10));

        // Warm up, so the measurement covers the work rather than the JIT.
        for (int i = 0; i < 1_000; ++i)
        {
            EnrolmentToken.TryValidate(Secret, token, "production", Now);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        const int Iterations = 10_000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool accepted = true;
        for (int i = 0; i < Iterations; ++i)
        {
            accepted &= EnrolmentToken.TryValidate(Secret, token, "production", Now);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        accepted.ShouldBeTrue("the measured calls must be doing the real work, not failing early");
        allocated.ShouldBe(0, $"validation must allocate nothing; it allocated {allocated} bytes over {Iterations} calls");
    }

    [TestMethod]
    public void An_environment_spelled_with_the_separator_cannot_borrow_anothers_token()
    {
        // The message is unframed, so this is the collision to rule out: an environment name containing the separator
        // must not be able to produce the same signed message as a different (environment, expiry) pair.
        long expiry = Now.AddMinutes(10).ToUnixTimeSeconds();
        string token = EnrolmentToken.Issue(Secret, $"prod:{expiry}", Now.AddMinutes(10));

        EnrolmentToken.TryValidate(Secret, token, "prod", Now).ShouldBeFalse();
        EnrolmentToken.TryValidate(Secret, token, $"prod:{expiry}", Now).ShouldBeTrue();
    }
}