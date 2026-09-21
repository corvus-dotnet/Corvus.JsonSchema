// <copyright file="SigV4ServerlessInvokeAuthenticatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Amazon.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy.Tests;

/// <summary>
/// Proves <see cref="SigV4ServerlessInvokeAuthenticator"/> against the known answers of the AWS Signature Version 4 test
/// suite (credentials <c>AKIDEXAMPLE</c>, region <c>us-east-1</c>, service <c>service</c>, 2015-08-30T12:36:00Z), and
/// that what it signs is what the invocation carries. There is no local check of an <c>AWS_IAM</c> Function URL, since
/// LocalStack Community ignores IAM (ADR 0060), so the known answers are what pins the signature.
/// </summary>
[TestClass]
public sealed class SigV4ServerlessInvokeAuthenticatorTests
{
    private const string AccessKey = "AKIDEXAMPLE";
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY";

    private static readonly DateTimeOffset SuiteTime = new(2015, 8, 30, 12, 36, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Signs_the_suites_get_vanilla_request_to_its_known_signature()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.amazonaws.com/");

        await SuiteSigner().AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        Header(request, "Authorization").ShouldBe(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20150830/us-east-1/service/aws4_request, SignedHeaders=host;x-amz-date, Signature=5fa00fa31553b73ebf1942676e86291e8372ff2a2260956d9b8aae1d763fbf31");
        Header(request, "x-amz-date").ShouldBe("20150830T123600Z");
    }

    [TestMethod]
    public async Task Signs_the_suites_post_vanilla_request_to_its_known_signature()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.amazonaws.com/");

        await SuiteSigner().AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        Header(request, "Authorization").ShouldEndWith("Signature=5da7c1a2acd57cee7505fc6676e4e544621c30862966e37dddb68e92efbe5d6b");
    }

    [TestMethod]
    public async Task The_signature_covers_the_payload()
    {
        using var one = new HttpRequestMessage(HttpMethod.Post, "https://abc.lambda-url.eu-west-1.on.aws/");
        using var other = new HttpRequestMessage(HttpMethod.Post, "https://abc.lambda-url.eu-west-1.on.aws/");
        SigV4ServerlessInvokeAuthenticator signer = SuiteSigner();

        await signer.AuthenticateAsync(one, Encoding.UTF8.GetBytes("""{"runId":"a"}"""), default);
        await signer.AuthenticateAsync(other, Encoding.UTF8.GetBytes("""{"runId":"b"}"""), default);

        // An invocation naming another run cannot reuse this one's signature.
        Header(one, "Authorization").ShouldNotBe(Header(other, "Authorization"));
    }

    [TestMethod]
    public async Task A_session_token_is_carried_and_signed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://abc.lambda-url.eu-west-1.on.aws/");
        var signer = new SigV4ServerlessInvokeAuthenticator(new SessionAWSCredentials(AccessKey, SecretKey, "session-token"), "eu-west-1", new FixedTimeProvider(SuiteTime));

        await signer.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        Header(request, "x-amz-security-token").ShouldBe("session-token");
        Header(request, "Authorization").ShouldContain("Credential=AKIDEXAMPLE/20150830/eu-west-1/lambda/aws4_request, SignedHeaders=host;x-amz-date;x-amz-security-token,");
    }

    [TestMethod]
    public async Task Signing_again_replaces_the_credential_rather_than_adding_a_second()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.amazonaws.com/");
        SigV4ServerlessInvokeAuthenticator signer = SuiteSigner();

        await signer.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);
        await signer.AuthenticateAsync(request, ReadOnlyMemory<byte>.Empty, default);

        request.Headers.GetValues("Authorization").Count().ShouldBe(1);
        request.Headers.GetValues("x-amz-date").Count().ShouldBe(1);
    }

    [TestMethod]
    public void Rejects_missing_constructor_arguments()
    {
        Should.Throw<ArgumentNullException>(() => new SigV4ServerlessInvokeAuthenticator(null!, "us-east-1"));
        Should.Throw<ArgumentException>(() => new SigV4ServerlessInvokeAuthenticator(new BasicAWSCredentials(AccessKey, SecretKey), string.Empty));
    }

    private static SigV4ServerlessInvokeAuthenticator SuiteSigner()
        => new(new BasicAWSCredentials(AccessKey, SecretKey), "us-east-1", new FixedTimeProvider(SuiteTime), service: "service");

    private static string Header(HttpRequestMessage request, string name) => request.Headers.GetValues(name).Single();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}