// <copyright file="SigV4ServerlessInvokeAuthenticator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;

/// <summary>
/// Signs the runner's invocation of a Lambda Function URL with AWS Signature Version 4, which is what a Function URL
/// created with <c>AWS_IAM</c> authorization requires (ADR 0059 decision 4). <see cref="LambdaServerlessDeployer"/>
/// creates every Function URL that way, so without this signature AWS refuses the invocation.
/// </summary>
/// <remarks>
/// The credentials are the runner's, supplied for the environment, and are read for each invocation so that a rotating
/// credential (an assumed role, an instance profile) is honoured. The signature covers the method, the path and query,
/// the <c>host</c> and <c>x-amz-date</c> headers, the session token when there is one, and the hash of the exact payload.
/// The principal the credentials belong to needs <c>lambda:InvokeFunctionUrl</c> on the function.
/// </remarks>
public sealed class SigV4ServerlessInvokeAuthenticator : IServerlessInvokeAuthenticator
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Terminator = "aws4_request";

    private readonly AWSCredentials credentials;
    private readonly string region;
    private readonly string service;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="SigV4ServerlessInvokeAuthenticator"/> class.</summary>
    /// <param name="credentials">The runner's AWS credentials for the environment.</param>
    /// <param name="region">The AWS region the function is deployed in, which is part of the signing scope.</param>
    /// <param name="timeProvider">The clock the signature is dated from, or <see langword="null"/> for the system clock.</param>
    /// <param name="service">The AWS service name in the signing scope. A Function URL signs for <c>lambda</c>.</param>
    public SigV4ServerlessInvokeAuthenticator(AWSCredentials credentials, string region, TimeProvider? timeProvider = null, string service = "lambda")
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrEmpty(region);
        ArgumentException.ThrowIfNullOrEmpty(service);
        this.credentials = credentials;
        this.region = region;
        this.service = service;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Uri url = request.RequestUri is { IsAbsoluteUri: true } absolute
            ? absolute
            : throw new InvalidOperationException("A Signature Version 4 signature needs the invocation's absolute URL.");

        ImmutableCredentials keys = await this.credentials.GetCredentialsAsync().ConfigureAwait(false);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string amzDate = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string date = amzDate[..8];
        bool hasToken = !string.IsNullOrEmpty(keys.Token);

        // The headers are strings because that is what HttpRequestMessage carries. The signed set is fixed and already
        // in the sorted order the canonical form needs.
        string signedHeaders = hasToken ? "host;x-amz-date;x-amz-security-token" : "host;x-amz-date";
        var canonical = new StringBuilder(256);
        canonical.Append(request.Method.Method).Append('\n');
        AppendCanonicalPath(canonical, url);
        canonical.Append('\n');
        AppendCanonicalQuery(canonical, url);
        canonical.Append('\n');
        canonical.Append("host:").Append(url.Authority).Append('\n');
        canonical.Append("x-amz-date:").Append(amzDate).Append('\n');
        if (hasToken)
        {
            canonical.Append("x-amz-security-token:").Append(keys.Token).Append('\n');
        }

        canonical.Append('\n');
        canonical.Append(signedHeaders).Append('\n');
        AppendHexSha256(canonical, body.Span);

        string scope = $"{date}/{this.region}/{this.service}/{Terminator}";
        var toSign = new StringBuilder(160);
        toSign.Append(Algorithm).Append('\n').Append(amzDate).Append('\n').Append(scope).Append('\n');
        AppendHexSha256(toSign, Encoding.UTF8.GetBytes(canonical.ToString()));

        Span<byte> key = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + keys.SecretKey), Encoding.UTF8.GetBytes(date), key);
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(this.region), key);
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(this.service), key);
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Terminator), key);
        Span<byte> signature = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign.ToString()), signature);
        CryptographicOperations.ZeroMemory(key);

        request.Headers.Remove("x-amz-date");
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        if (hasToken)
        {
            request.Headers.Remove("x-amz-security-token");
            request.Headers.TryAddWithoutValidation("x-amz-security-token", keys.Token);
        }

        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"{Algorithm} Credential={keys.AccessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={Convert.ToHexStringLower(signature)}");
    }

    // Every service but S3 encodes each path segment twice: the URL's own escaping, then again for the canonical form.
    private static void AppendCanonicalPath(StringBuilder canonical, Uri url)
    {
        string path = url.AbsolutePath;
        if (path.Length == 0)
        {
            canonical.Append('/');
            return;
        }

        int start = 0;
        while (start <= path.Length)
        {
            int slash = path.IndexOf('/', start);
            int end = slash < 0 ? path.Length : slash;
            if (end > start)
            {
                canonical.Append(Uri.EscapeDataString(path[start..end]));
            }

            if (slash < 0)
            {
                break;
            }

            canonical.Append('/');
            start = slash + 1;
        }
    }

    // Parameters sorted by encoded name then value, each encoded once from its decoded form.
    private static void AppendCanonicalQuery(StringBuilder canonical, Uri url)
    {
        string query = url.Query;
        if (query.Length <= 1)
        {
            return;
        }

        string[] pairs = query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries);
        var encoded = new (string Name, string Value)[pairs.Length];
        for (int i = 0; i < pairs.Length; i++)
        {
            int equals = pairs[i].IndexOf('=');
            string name = equals < 0 ? pairs[i] : pairs[i][..equals];
            string value = equals < 0 ? string.Empty : pairs[i][(equals + 1)..];
            encoded[i] = (Uri.EscapeDataString(Uri.UnescapeDataString(name)), Uri.EscapeDataString(Uri.UnescapeDataString(value)));
        }

        Array.Sort(encoded, static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Value, b.Value);
        });

        for (int i = 0; i < encoded.Length; i++)
        {
            if (i > 0)
            {
                canonical.Append('&');
            }

            canonical.Append(encoded[i].Name).Append('=').Append(encoded[i].Value);
        }
    }

    private static void AppendHexSha256(StringBuilder target, ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(data, hash);
        Span<char> hex = stackalloc char[SHA256.HashSizeInBytes * 2];
        Convert.TryToHexStringLower(hash, hex, out _);
        target.Append(hex);
    }
}