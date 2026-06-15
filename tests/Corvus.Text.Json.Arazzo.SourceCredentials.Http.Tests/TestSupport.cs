// <copyright file="TestSupport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.SourceCredentials.Http.Tests;

/// <summary>A manually-advanced <see cref="TimeProvider"/> for deterministic TTL/expiry tests.</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => this.now;

    public void Advance(TimeSpan by) => this.now += by;
}

/// <summary>An <see cref="ISecretResolver"/> stub that returns configured secrets and records every material it issues
/// (so a test can assert the factory scrubbed it).</summary>
public sealed class FakeSecretResolver : ISecretResolver
{
    private readonly Dictionary<string, string> secrets;

    public FakeSecretResolver(Dictionary<string, string> secrets) => this.secrets = secrets;

    public List<SecretMaterial> Issued { get; } = [];

    public int ResolveCount { get; private set; }

    public bool CanResolve(SecretScheme scheme) => true;

    public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        this.ResolveCount++;
        if (!this.secrets.TryGetValue(reference.Raw, out string? value))
        {
            throw new SecretResolutionException(reference, "no such secret in the stub.");
        }

        var material = SecretMaterial.FromString(value);
        this.Issued.Add(material);
        return new ValueTask<SecretMaterial>(material);
    }
}

/// <summary>A fake <see cref="HttpMessageHandler"/> that returns a fixed response and counts/records requests.</summary>
public sealed class MockHttpHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    public List<string> RequestBodies { get; } = [];

    public Func<HttpRequestMessage, Task>? OnRequest { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.RequestCount++;
        if (request.Content is not null)
        {
            this.RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }

        if (this.OnRequest is not null)
        {
            await this.OnRequest(request).ConfigureAwait(false);
        }

        return new HttpResponseMessage(statusCode) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(responseBody)) };
    }
}

/// <summary>Helpers for building <see cref="SourceCredentialBinding"/> values directly in tests.</summary>
public static class BindingFactory
{
    public static SourceCredentialBinding Create(SourceCredentialDefinition definition, string id = "scred-1", string etag = "1")
        => SourceCredentialBinding.FromJson(
            SourceCredentialSerialization.SerializeNew(id, definition, "tester", DateTimeOffset.UnixEpoch, new WorkflowEtag(etag)));
}