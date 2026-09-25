// <copyright file="SealedStartTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The sealed start's binding, signature and initiator library (ADR 0065 decision 9), and the runner key ring's
/// seal key and pinned initiators.
/// </summary>
[TestClass]
public sealed class SealedStartTests
{
    private const string RunId = "0123456789abcdef0123456789abcdef";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();

    [TestMethod]
    public void The_binding_frames_every_field_so_no_other_start_shares_it()
    {
        byte[] binding = Binding("production", "onboard", 2, "k1", RunId);

        // len‖env ‖ len‖base ‖ uint32(version) ‖ len‖keyId ‖ len‖runId
        byte[] expected =
        [
            0, 0, 0, 10, .. Encoding.UTF8.GetBytes("production"),
            0, 0, 0, 7, .. Encoding.UTF8.GetBytes("onboard"),
            0, 0, 0, 2,
            0, 0, 0, 2, .. Encoding.UTF8.GetBytes("k1"),
            0, 0, 0, 32, .. Encoding.UTF8.GetBytes(RunId),
        ];
        binding.ShouldBe(expected);
        Binding("production", "onboard", 2, "k1", RunId).ShouldBe(binding, "deterministic");
        Binding("staging", "onboard", 2, "k1", RunId).ShouldNotBe(binding, "another environment");
        Binding("production", "onboard", 3, "k1", RunId).ShouldNotBe(binding, "another version");
        Binding("production", "onboard", 2, "k2", RunId).ShouldNotBe(binding, "another generation");
        Binding("production", "onboar", 2, "dk1", RunId).ShouldNotBe(binding, "framed, so a different split of the same bytes differs");
        Should.Throw<ArgumentException>(() => Binding("production", new string('w', 257), 2, "k1", RunId));
    }

    [TestMethod]
    public void The_signature_verifies_under_the_initiators_key_over_exactly_the_binding_and_the_seal()
    {
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] binding = Binding("production", "onboard", 2, "k1", RunId);
        byte[] enc = RandomNumberGenerator.GetBytes(InputSeal.EncLength);
        byte[] ciphertext = RandomNumberGenerator.GetBytes(40);
        byte[] signature = new byte[SealedStartSignature.SignatureLength];

        SealedStartSignature.Sign(initiator, binding, enc, ciphertext, signature);

        byte[] pinned = initiator.ExportSubjectPublicKeyInfo();
        SealedStartSignature.Verify(pinned, binding, enc, ciphertext, signature).ShouldBeTrue();
        SealedStartSignature.Verify(other.ExportSubjectPublicKeyInfo(), binding, enc, ciphertext, signature).ShouldBeFalse("another key");
        SealedStartSignature.Verify(pinned, Binding("production", "onboard", 2, "k1", "fedcba9876543210fedcba9876543210"), enc, ciphertext, signature).ShouldBeFalse("another run's binding");
        byte[] otherEnc = [.. enc];
        otherEnc[5] ^= 1;
        SealedStartSignature.Verify(pinned, binding, otherEnc, ciphertext, signature).ShouldBeFalse("another encapsulated key");
        byte[] otherCiphertext = [.. ciphertext];
        otherCiphertext[5] ^= 1;
        SealedStartSignature.Verify(pinned, binding, enc, otherCiphertext, signature).ShouldBeFalse("another ciphertext");
        SealedStartSignature.Verify(pinned, binding, enc, ciphertext, signature[..63]).ShouldBeFalse("a short signature");
        SealedStartSignature.Verify([1, 2, 3], binding, enc, ciphertext, signature).ShouldBeFalse("a pinned key that is no key");

        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Should.Throw<CryptographicException>(() => SealedStartSignature.Sign(p384, binding, enc, ciphertext, signature), "only ES256 signs a start");
        SealedStartSignature.IsP256PublicKey(pinned).ShouldBeTrue();
        SealedStartSignature.IsP256PublicKey(p384.ExportSubjectPublicKeyInfo()).ShouldBeFalse();
    }

    [TestMethod]
    public void An_initiator_seals_and_signs_what_the_runner_opens_and_verifies()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = InputSealTests.SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] inputs = Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}""");

        SealedInputs sealedInputs = RunStartInitiator.Seal(sealSpki, "k1", "production", "onboard", 2, RunId, inputs, initiator);

        sealedInputs.IsWellFormed.ShouldBeTrue();
        sealedInputs.KeyId.ShouldBe("k1");
        sealedInputs.Ciphertext.Length.ShouldBe(inputs.Length + InputSeal.TagLength);
        byte[] binding = Binding("production", "onboard", 2, "k1", RunId);
        SealedStartSignature.Verify(initiator.ExportSubjectPublicKeyInfo(), binding, sealedInputs.Enc.Span, sealedInputs.Ciphertext.Span, sealedInputs.Signature.Span).ShouldBeTrue();
        byte[] opened = new byte[inputs.Length];
        InputSeal.Open(sealPkcs8, sealedInputs.Enc.Span, SealedStartSignature.SealInfo, binding, sealedInputs.Ciphertext.Span, opened);
        opened.ShouldBe(inputs);

        Should.Throw<ArgumentException>(() => RunStartInitiator.Seal(sealSpki, "k1", "production", "onboard", 2, "not-a-run-id", inputs, initiator), "the initiator names the run inside the grammar");
        RunStartInitiator.NewRunId().ShouldMatch("^[0-9a-f]{32}$");
        RunStartInitiator.NewRunId().ShouldNotBe(RunStartInitiator.NewRunId());
        RunStartInitiator.SealKeyFingerprint(sealSpki).ShouldBe(Convert.ToBase64String(SHA256.HashData(sealSpki)));
    }

    [TestMethod]
    public void The_wire_form_round_trips_the_seal_byte_for_byte()
    {
        (byte[] sealSpki, _) = InputSealTests.SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SealedInputs sealedInputs = RunStartInitiator.Seal(sealSpki, "k1", "production", "onboard", 2, RunId, "{}"u8, initiator);

        byte[] wire = SealedRunStart.Serialize(RunId, sealedInputs);
        using ParsedJsonDocument<SealedRunStart> parsed = ParsedJsonDocument<SealedRunStart>.Parse(wire);

        parsed.RootElement.Signature.IsNotUndefined().ShouldBeTrue();
        ((string)parsed.RootElement.RunId).ShouldBe(RunId);
        SealedInputs back = parsed.RootElement.ToSealedInputs();
        back.KeyId.ShouldBe("k1");
        back.Enc.ToArray().ShouldBe(sealedInputs.Enc.ToArray());
        back.Ciphertext.ToArray().ShouldBe(sealedInputs.Ciphertext.ToArray());
        back.Signature.ToArray().ShouldBe(sealedInputs.Signature.ToArray());

        using ParsedJsonDocument<SealedRunStart> short_ = ParsedJsonDocument<SealedRunStart>.Parse("""{"runId":"0123456789abcdef0123456789abcdef","keyId":"k1","enc":"AAEC","ciphertext":"AAEC","signature":"AAEC"}"""u8.ToArray());
        Should.Throw<FormatException>(() => short_.RootElement.ToSealedInputs(), "parts that are not a seal's shapes");
    }

    [TestMethod]
    public async Task The_ring_reads_the_seal_key_and_pins_the_initiators_from_the_runners_own_configuration()
    {
        (_, byte[] sealPkcs8) = InputSealTests.SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string initiatorSpki = Convert.ToBase64String(initiator.ExportSubjectPublicKeyInfo());
        var secrets = new MapSecretResolver(new Dictionary<string, string>
        {
            ["PAYLOAD_KEY"] = Convert.ToBase64String(PayloadKey),
            ["SEAL_KEY"] = Convert.ToBase64String(sealPkcs8),
        });

        RunnerKeyRing ring = await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true, SecretRef.Parse("env://SEAL_KEY"), [initiatorSpki])], secrets, default);

        ring.TryGet("production", out RunnerEnvironmentKeys keys).ShouldBeTrue();
        keys.OpensSealedStarts.ShouldBeTrue();
        keys.SealPrivateKey.ShouldBe(sealPkcs8);
        keys.InitiatorKeys!.Single().ShouldBe(initiator.ExportSubjectPublicKeyInfo());

        // Without a seal key the ring is as before: the environment is sealed, and sealed starts there fault.
        RunnerKeyRing plain = await RunnerKeyRing.BuildAsync([new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true)], secrets, default);
        plain.TryGet("production", out RunnerEnvironmentKeys plainKeys).ShouldBeTrue();
        plainKeys.OpensSealedStarts.ShouldBeFalse();

        // Half a configuration is no configuration: a seal key opens nothing without a pinned initiator, and a pin
        // without a seal key is meaningless.
        (await Should.ThrowAsync<InvalidOperationException>(async () => await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true, SecretRef.Parse("env://SEAL_KEY"))], secrets, default))).Message.ShouldContain("initiator");
        (await Should.ThrowAsync<InvalidOperationException>(async () => await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true, Initiators: [initiatorSpki])], secrets, default))).Message.ShouldContain("initiator");

        // A seal key that is not a P-256 key, and an initiator that is not a P-256 public key, refuse the build.
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var wrongKeys = new MapSecretResolver(new Dictionary<string, string>
        {
            ["PAYLOAD_KEY"] = Convert.ToBase64String(PayloadKey),
            ["SEAL_KEY"] = Convert.ToBase64String(p384.ExportPkcs8PrivateKey()),
        });
        (await Should.ThrowAsync<InvalidOperationException>(async () => await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true, SecretRef.Parse("env://SEAL_KEY"), [initiatorSpki])], wrongKeys, default))).Message.ShouldContain("P-256");
        (await Should.ThrowAsync<InvalidOperationException>(async () => await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry("production", "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true, SecretRef.Parse("env://SEAL_KEY"), [Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo())])], secrets, default))).Message.ShouldContain("P-256");
    }

    private static byte[] Binding(string environment, string baseWorkflowId, int version, string keyId, string runId)
    {
        byte[] binding = new byte[SealedStartSignature.BindingLength(environment, baseWorkflowId, keyId, runId)];
        SealedStartSignature.WriteBinding(environment, baseWorkflowId, version, keyId, runId, binding).ShouldBe(binding.Length);
        return binding;
    }

    private sealed class MapSecretResolver(IReadOnlyDictionary<string, string> secrets) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => new(SecretMaterial.FromString(secrets[reference.Locator]));
    }
}